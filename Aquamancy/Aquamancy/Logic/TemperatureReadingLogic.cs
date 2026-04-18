using Aquamancy.Dto;
using Aquamancy.IData;
using Aquamancy.ILogic;
using Aquamancy.Models;
using System.Globalization;

namespace Aquamancy.Logic
{
    public class TemperatureReadingLogic(IProbeRepository probeRepo, ITemperatureRepository tempRepo) : ITemperatureReadingLogic
    {
        private readonly IProbeRepository _probeRepo = probeRepo;
        private readonly ITemperatureRepository _tempRepo = tempRepo;

        public async Task<(bool Success, string? ErrorMessage, Probe Probe, double Temperature)> Insert(PostParams data, Probe probe)
        {


            // Parse the temperature value
            if (!double.TryParse(data.Temperature, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var temp))
            {
                // Try with current culture as a fallback
                if (!double.TryParse(data.Temperature, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out temp))
                {
                    return (false, "Invalid temperature value", probe, 0);
                }
            }

            // Round to the nearest upper 0.5 to prevent micro variations from the probe
            temp = Math.Round(temp * 2, MidpointRounding.AwayFromZero) / 2;

            var reading = new TemperatureReading
            {
                ProbeId = probe.Id,
                Temperature = temp,
                Timestamp = DateTime.Now
            };

            await _tempRepo.AddAsync(reading);


            return (true, null, probe, temp);
        }
    }
}
