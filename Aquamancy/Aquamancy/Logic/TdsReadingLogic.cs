using Aquamancy.Dto;
using Aquamancy.IData;
using Aquamancy.ILogic;
using Aquamancy.Models;
using System.Globalization;

namespace Aquamancy.Logic
{
    public class TdsReadingLogic(ITdsRepository tdsRepo) : ITdsReadingLogic
    {
        private readonly ITdsRepository _tdsRepo = tdsRepo;

        public async Task<(bool Success, string? ErrorMessage, Probe Probe)> Insert(PostParams data, Probe probe)
        {
            // Parse the Tds value
            if (!double.TryParse(data.Tds, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var tds))
            {
                // Try with current culture as a fallback
                if (!double.TryParse(data.Tds, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out tds))
                {
                    return (false, "Invalid Tds value", probe);
                }
            }

            var reading = new TdsReading
            {
                ProbeId = probe.Id,
                Tds = tds,
                Timestamp = DateTime.Now
            };

            await _tdsRepo.AddAsync(reading);

            return (true, null, probe);
        }
    }
}
