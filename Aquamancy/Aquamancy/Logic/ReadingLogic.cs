using Aquamancy.Dto;
using Aquamancy.IData;
using Aquamancy.ILogic;
using Aquamancy.Models;

namespace Aquamancy.Logic
{
    public class ReadingLogic(IProbeRepository probeRepository,
        IDiscordNotifierLogic discordNotifierLogic,
        ITemperatureReadingLogic temperatureReadingLogic,
        ITurbidityReadingLogic turbidityReadingLogic) : IReadingLogic
    {
        private readonly IProbeRepository _probeRepo = probeRepository;
        private readonly ITemperatureReadingLogic _temperatureReadingLogic = temperatureReadingLogic;
        private readonly ITurbidityReadingLogic _turbidityReadingLogic = turbidityReadingLogic;
        private readonly IDiscordNotifierLogic _discordNotifierLogic = discordNotifierLogic;

        public async Task<(bool Success, string? ErrorMessage, Probe Probe)> Insert(PostParams postParams)
        {

            // Find the probe by machine name (case-insensitive match)
            var probes = await _probeRepo.GetAllAsync();
            var probe = probes.FirstOrDefault(p => string.Equals(p.MachineName?.Trim(), postParams.MachineName.Trim(), System.StringComparison.OrdinalIgnoreCase));
            if (probe is null)
            {
                // create probe with the given machine name
                var machine = postParams.MachineName.Trim();
                var newProbe = new Probe
                {
                    Name = machine,
                    MachineName = machine,
                    Color = $"#{Random.Shared.Next(0x1000000):X6}",
                    MinTemperature = 0,
                    MaxTemperature = 0,
                    CreatedAt = DateTime.Now,
                    Rssi = postParams.Rssi,
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
                await _probeRepo.UpdateCommunicationInfoAsync(probe.Id, postParams.Rssi, DateTime.Now, postParams.FirstLoop ? DateTime.Now : null);
                probe.Rssi = postParams.Rssi;
                probe.LastCommunicationDate = DateTime.Now;
            }

            var tempResult = await _temperatureReadingLogic.Insert(postParams, probe);
            if (!tempResult.Success)
            {
                return (false, tempResult.ErrorMessage, probe);
            }

            // Turbidity is optional for now, only for testing
            if (!string.IsNullOrWhiteSpace(postParams.Turbidity))
            {
                var turbidityResult = await _turbidityReadingLogic.Insert(postParams, probe);
                if (!turbidityResult.Success)
                {
                    return (false, turbidityResult.ErrorMessage, probe);
                }
            }

            return (true, null, probe);
        }
    }
}
