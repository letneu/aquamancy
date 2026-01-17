using Aquamancy.IData;
using Aquamancy.Models;

namespace Aquamancy.Data
{
    public class ProbeRepository : IProbeRepository
    {
        private readonly IDbConnectionFactory _factory;

        public ProbeRepository(IDbConnectionFactory factory)
        {
            _factory = factory;
        }

        public async Task EnsureTableExistsAsync()
        {
            await using var conn = _factory.CreateConnection();
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS probes (
                id INT AUTO_INCREMENT PRIMARY KEY,
                name VARCHAR(200) NOT NULL,
                machine_name VARCHAR(200) DEFAULT '',
                color VARCHAR(20) DEFAULT '#007bff',
                min_temperature DOUBLE DEFAULT NULL,
                max_temperature DOUBLE DEFAULT NULL,
                send_frequency_in_seconds INT DEFAULT 60,
                tendency_span_hours INT DEFAULT 2,
                minimum_tendency_change DOUBLE DEFAULT 0.3,
                created_at DATETIME NOT NULL,
                last_notified_at DATETIME DEFAULT NULL,
                last_communication_date DATETIME DEFAULT NULL,
                rssi INT DEFAULT 0
            );";

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<int> AddAsync(Probe probe)
        {
            await using var conn = _factory.CreateConnection();
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO probes (name, machine_name, color, min_temperature, max_temperature, send_frequency_in_seconds, tendency_span_hours, minimum_tendency_change, created_at, last_notified_at, last_communication_date, rssi) VALUES (@name, @machine_name, @color, @min_temperature, @max_temperature, @send_frequency_in_seconds, @tendency_span_hours, @minimum_tendency_change, @created_at, @last_notified_at, @last_communication_date, @rssi); SELECT LAST_INSERT_ID();";

            var p1 = cmd.CreateParameter(); p1.ParameterName = "@name"; p1.Value = probe.Name; cmd.Parameters.Add(p1);
            var p2 = cmd.CreateParameter(); p2.ParameterName = "@machine_name"; p2.Value = probe.MachineName; cmd.Parameters.Add(p2);
            var p3 = cmd.CreateParameter(); p3.ParameterName = "@color"; p3.Value = probe.Color; cmd.Parameters.Add(p3);
            var p4 = cmd.CreateParameter(); p4.ParameterName = "@min_temperature"; p4.Value = probe.MinTemperature != 0 ? (object)probe.MinTemperature : System.DBNull.Value; cmd.Parameters.Add(p4);
            var p5 = cmd.CreateParameter(); p5.ParameterName = "@max_temperature"; p5.Value = probe.MaxTemperature != 0 ? (object)probe.MaxTemperature : System.DBNull.Value; cmd.Parameters.Add(p5);
            var p6 = cmd.CreateParameter(); p6.ParameterName = "@send_frequency_in_seconds"; p6.Value = probe.SendFrequencyInSeconds; cmd.Parameters.Add(p6);
            var p7 = cmd.CreateParameter(); p7.ParameterName = "@tendency_span_hours"; p7.Value = probe.TendencySpanHours; cmd.Parameters.Add(p7);
            var p8 = cmd.CreateParameter(); p8.ParameterName = "@minimum_tendency_change"; p8.Value = probe.MinimumTendencyChange; cmd.Parameters.Add(p8);
            var p9 = cmd.CreateParameter(); p9.ParameterName = "@created_at"; p9.Value = probe.CreatedAt; cmd.Parameters.Add(p9);
            var p10 = cmd.CreateParameter(); p10.ParameterName = "@last_notified_at"; p10.Value = probe.LastNotifiedAt.HasValue ? (object)probe.LastNotifiedAt.Value : System.DBNull.Value; cmd.Parameters.Add(p10);
            var p11 = cmd.CreateParameter(); p11.ParameterName = "@last_communication_date"; p11.Value = probe.LastCommunicationDate.HasValue ? (object)probe.LastCommunicationDate.Value : System.DBNull.Value; cmd.Parameters.Add(p11);
            var p12 = cmd.CreateParameter(); p12.ParameterName = "@rssi"; p12.Value = probe.Rssi; cmd.Parameters.Add(p12);

            var res = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(res);
        }

        public async Task<Probe?> GetByIdAsync(int id)
        {
            await using var conn = _factory.CreateConnection();
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, name, machine_name, color, min_temperature, max_temperature, send_frequency_in_seconds, tendency_span_hours, minimum_tendency_change, created_at, last_notified_at, last_communication_date, rssi FROM probes WHERE id = @id LIMIT 1";
            var p = cmd.CreateParameter(); p.ParameterName = "@id"; p.Value = id; cmd.Parameters.Add(p);

            await using var rdr = await cmd.ExecuteReaderAsync();
            if (await rdr.ReadAsync())
            {
                return new Probe
                {
                    Id = rdr.GetInt32(0),
                    Name = rdr.GetString(1),
                    MachineName = rdr.GetString(2),
                    Color = rdr.GetString(3),
                    MinTemperature = rdr.IsDBNull(4) ? 0 : rdr.GetDouble(4),
                    MaxTemperature = rdr.IsDBNull(5) ? 0 : rdr.GetDouble(5),
                    SendFrequencyInSeconds = rdr.IsDBNull(6) ? 60 : rdr.GetInt32(6),
                    TendencySpanHours = rdr.IsDBNull(7) ? 2 : rdr.GetInt32(7),
                    MinimumTendencyChange = rdr.IsDBNull(8) ? 0.3 : rdr.GetDouble(8),
                    CreatedAt = rdr.GetDateTime(9),
                    LastNotifiedAt = rdr.IsDBNull(10) ? (DateTime?)null : rdr.GetDateTime(10),
                    LastCommunicationDate = rdr.IsDBNull(11) ? (DateTime?)null : rdr.GetDateTime(11),
                    Rssi = rdr.IsDBNull(12) ? 0 : rdr.GetInt32(12)
                };
            }

            return null;
        }

        public async Task<IEnumerable<Probe>> GetAllAsync()
        {
            var list = new List<Probe>();

            await using var conn = _factory.CreateConnection();
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, name, machine_name, color, min_temperature, max_temperature, send_frequency_in_seconds, tendency_span_hours, minimum_tendency_change, created_at, last_notified_at, last_communication_date, rssi FROM probes ORDER BY id";

            await using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                list.Add(new Probe
                {
                    Id = rdr.GetInt32(0),
                    Name = rdr.GetString(1),
                    MachineName = rdr.GetString(2),
                    Color = rdr.GetString(3),
                    MinTemperature = rdr.IsDBNull(4) ? 0 : rdr.GetDouble(4),
                    MaxTemperature = rdr.IsDBNull(5) ? 0 : rdr.GetDouble(5),
                    SendFrequencyInSeconds = rdr.IsDBNull(6) ? 60 : rdr.GetInt32(6),
                    TendencySpanHours = rdr.IsDBNull(7) ? 2 : rdr.GetInt32(7),
                    MinimumTendencyChange = rdr.IsDBNull(8) ? 0.3 : rdr.GetDouble(8),
                    CreatedAt = rdr.GetDateTime(9),
                    LastNotifiedAt = rdr.IsDBNull(10) ? (DateTime?)null : rdr.GetDateTime(10),
                    LastCommunicationDate = rdr.IsDBNull(11) ? (DateTime?)null : rdr.GetDateTime(11),
                    Rssi = rdr.IsDBNull(12) ? 0 : rdr.GetInt32(12)
                });
            }

            return list;
        }

        public async Task UpdateLastNotifiedAsync(int probeId, System.DateTime? when)
        {
            await using var conn = _factory.CreateConnection();
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE probes SET last_notified_at = @when WHERE id = @id";
            var p1 = cmd.CreateParameter(); p1.ParameterName = "@when"; p1.Value = when.HasValue ? (object)when.Value : System.DBNull.Value; cmd.Parameters.Add(p1);
            var p2 = cmd.CreateParameter(); p2.ParameterName = "@id"; p2.Value = probeId; cmd.Parameters.Add(p2);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task UpdateCommunicationInfoAsync(int probeId, int rssi, System.DateTime lastCommunicationDate)
        {
            await using var conn = _factory.CreateConnection();
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE probes SET rssi = @rssi, last_communication_date = @lastCommunicationDate WHERE id = @id";
            var p1 = cmd.CreateParameter(); p1.ParameterName = "@rssi"; p1.Value = rssi; cmd.Parameters.Add(p1);
            var p2 = cmd.CreateParameter(); p2.ParameterName = "@lastCommunicationDate"; p2.Value = lastCommunicationDate; cmd.Parameters.Add(p2);
            var p3 = cmd.CreateParameter(); p3.ParameterName = "@id"; p3.Value = probeId; cmd.Parameters.Add(p3);

            await cmd.ExecuteNonQueryAsync();
        }
    }
}
