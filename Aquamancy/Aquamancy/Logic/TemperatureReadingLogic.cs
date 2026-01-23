using Aquamancy.IData;
using Aquamancy.ILogic;
using Aquamancy.Models;
using System.Globalization;
using static Aquamancy.Controllers.ApiController;

namespace Aquamancy.Logic
{
    public class TemperatureReadingLogic(IProbeRepository probeRepo, ITemperatureRepository tempRepo, IDiscordNotifierLogic discordNotifierLogic) : ITemperatureReadingLogic
    {
        private readonly IProbeRepository _probeRepo = probeRepo;
        private readonly ITemperatureRepository _tempRepo = tempRepo;
        private readonly IDiscordNotifierLogic _discordNotifierLogic = discordNotifierLogic;

        public async Task<(bool Success, string? ErrorMessage, Probe Probe, double Temperature)> Insert(PostParams data)
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
                    Color = $"#{Random.Shared.Next(0x1000000):X6}",
                    MinTemperature = 0,
                    MaxTemperature = 0,
                    CreatedAt = DateTime.Now,
                    Rssi = data.Rssi,
                    LastCommunicationDate = DateTime.Now
                };

                var newId = await _probeRepo.AddAsync(newProbe);
                newProbe.Id = newId;
                probe = newProbe;

                // Warn on discord
                await _discordNotifierLogic.SendDiscordMessageAsync($"Une nouvelle sonde a été ajoutée : {machine}, terminez la configuration depuis la table Probes avec l'id {newId}");
            }
            else
            {
                // Update RSSI and LastCommunicationDate for existing probe
                await _probeRepo.UpdateCommunicationInfoAsync(probe.Id, data.Rssi, DateTime.Now, data.FirstLoop ? DateTime.Now : null);
                probe.Rssi = data.Rssi;
                probe.LastCommunicationDate = DateTime.Now;
            }

            // Parse the temperature value
            if (!double.TryParse(data.Temperature, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var temp))
            {
                // Try with current culture as a fallback
                if (!double.TryParse(data.Temperature, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out temp))
                {
                    return (false, "Invalid temperature value", probe, 0);
                }
            }

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
