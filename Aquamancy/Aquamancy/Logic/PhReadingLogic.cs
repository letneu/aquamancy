using Aquamancy.Dto;
using Aquamancy.IData;
using Aquamancy.ILogic;
using Aquamancy.Models;
using System.Globalization;

namespace Aquamancy.Logic
{
    public class PhReadingLogic(IPhRepository phRepo) : IPhReadingLogic
    {
        private readonly IPhRepository _phRepo = phRepo;

        public async Task<(bool Success, string? ErrorMessage, Probe Probe)> Insert(PostParams data, Probe probe)
        {
            // Parse the pH value
            if (!double.TryParse(data.Ph, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var ph))
            {
                // Try with current culture as a fallback
                if (!double.TryParse(data.Ph, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out ph))
                {
                    return (false, "Invalid pH value", probe);
                }
            }

            var reading = new PhReading
            {
                ProbeId = probe.Id,
                Ph = ph,
                Timestamp = DateTime.Now
            };

            await _phRepo.AddAsync(reading);

            return (true, null, probe);
        }
    }
}
