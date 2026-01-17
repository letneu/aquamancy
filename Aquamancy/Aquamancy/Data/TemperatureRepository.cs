using Aquamancy.IData;
using Aquamancy.Models;

namespace Aquamancy.Data
{
    public class TemperatureRepository(IDbConnectionFactory factory) : ITemperatureRepository
    {
        private readonly IDbConnectionFactory _factory = factory;

        public async Task EnsureTableExistsAsync()
        {
            await using var conn = _factory.CreateConnection();
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS temperature_readings (
                id INT AUTO_INCREMENT PRIMARY KEY,
                probe_id INT NOT NULL,
                timestamp DATETIME NOT NULL,
                temperature DOUBLE NOT NULL,
                FOREIGN KEY (probe_id) REFERENCES probes(id) ON DELETE CASCADE
            );";

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<int> AddAsync(TemperatureReading reading)
        {
            await using var conn = _factory.CreateConnection();
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO temperature_readings (probe_id, timestamp, temperature) VALUES (@probe_id, @timestamp, @temperature); SELECT LAST_INSERT_ID();";

            var p1 = cmd.CreateParameter(); p1.ParameterName = "@probe_id"; p1.Value = reading.ProbeId; cmd.Parameters.Add(p1);
            var p2 = cmd.CreateParameter(); p2.ParameterName = "@timestamp"; p2.Value = reading.Timestamp; cmd.Parameters.Add(p2);
            var p3 = cmd.CreateParameter(); p3.ParameterName = "@temperature"; p3.Value = reading.Temperature; cmd.Parameters.Add(p3);

            var res = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(res);
        }

        public async Task<IEnumerable<TemperatureReading>> GetForProbeAsync(int probeId, DateTime limit)
        {
            var list = new List<TemperatureReading>();

            await using var conn = _factory.CreateConnection();
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, probe_id, timestamp, temperature FROM temperature_readings WHERE probe_id = @probe_id and timestamp >= @limit ORDER BY timestamp DESC";
            var p1 = cmd.CreateParameter(); p1.ParameterName = "@probe_id"; p1.Value = probeId; cmd.Parameters.Add(p1);
            var p2 = cmd.CreateParameter(); p2.ParameterName = "@limit"; p2.Value = limit; cmd.Parameters.Add(p2);

            await using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                list.Add(new TemperatureReading
                {
                    Id = rdr.GetInt32(0),
                    ProbeId = rdr.GetInt32(1),
                    Timestamp = rdr.GetDateTime(2),
                    Temperature = rdr.GetDouble(3)
                });
            }

            return list;
        }
    }
}
