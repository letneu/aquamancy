using Aquamancy.Data;
using Aquamancy.Models;
using System.Globalization;
using static Aquamancy.Controllers.ApiController;

namespace Aquamancy.Logic
{
    public class TemperatureReadingLogic : ITemperatureReadingLogic
    {
        private readonly IProbeRepository _probeRepo;
        private readonly ITemperatureRepository _tempRepo;

        public TemperatureReadingLogic(IProbeRepository probeRepo, ITemperatureRepository tempRepo)
        {
            _probeRepo = probeRepo;
            _tempRepo = tempRepo;
        }

        public async Task<(bool Success, string ErrorMessage, Probe Probe, double Temperature)> Insert(PostParams data)
        {

            // Find the probe by machine name (case-insensitive match)
            var probes = await _probeRepo.GetAllAsync();
            var probe = probes.FirstOrDefault(p => string.Equals(p.MachineName?.Trim(), data.MachineName.Trim(), System.StringComparison.OrdinalIgnoreCase));
            if (probe is null)
            {
                // create probe with the given machine name
                var machine = data.MachineName.Trim();
                var newProbe = new Probe
                {
                    Name = machine,
                    MachineName = machine,
                    // generate a random hex color like #RRGGBB
                    Color = $"#{System.Random.Shared.Next(0x1000000):X6}",
                    MinTemperature = 0,
                    MaxTemperature = 0,
                    CreatedAt = System.DateTime.Now
                };

                var newId = await _probeRepo.AddAsync(newProbe);
                newProbe.Id = newId;
                probe = newProbe;
            }

            // Parse the temperature value
            if (!double.TryParse(data.Temperature, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var temp))
            {
                // Try with current culture as a fallback
                if (!double.TryParse(data.Temperature, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out temp))
                {
                    return (false, "Invalid temperature value", null, 0);
                }
            }

            var reading = new TemperatureReading
            {
                ProbeId = probe.Id,
                Temperature = temp,
                Timestamp = System.DateTime.Now
            };

            await _tempRepo.AddAsync(reading);

            return (true, null, probe, temp);
        }
    }
}
