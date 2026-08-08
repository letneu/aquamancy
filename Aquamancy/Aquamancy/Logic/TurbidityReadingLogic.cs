using Aquamancy.Dto;
using Aquamancy.IData;
using Aquamancy.ILogic;
using Aquamancy.Models;
using System.Globalization;

namespace Aquamancy.Logic
{
    public class TurbidityReadingLogic(ITurbidityRepository turbidityRepo) : ITurbidityReadingLogic
    {
        private readonly ITurbidityRepository _turbidityRepo = turbidityRepo;

        public async Task<(bool Success, string? ErrorMessage, Probe Probe)> Insert(PostParams data, Probe probe)
        {
            // Parse the Turbidity value
            if (!double.TryParse(data.Turbidity, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var turbidity))
            {
                // Try with current culture as a fallback
                if (!double.TryParse(data.Turbidity, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out turbidity))
                {
                    return (false, "Invalid Turbidity value", probe);
                }
            }

            var reading = new TurbidityReading
            {
                ProbeId = probe.Id,
                Turbidity = turbidity,
                Timestamp = DateTime.Now
            };

            await _turbidityRepo.AddAsync(reading);

            return (true, null, probe);
        }
    }
}
