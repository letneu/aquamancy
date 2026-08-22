using Aquamancy.Dto;
using Aquamancy.IData;
using Aquamancy.ILogic;
using Aquamancy.Models;

namespace Aquamancy.Logic
{
    public class ReadingLogic(IProbeRepository probeRepository,
        IDiscordNotifierLogic discordNotifierLogic,
        ITemperatureReadingLogic temperatureReadingLogic,
        ITdsReadingLogic tdsReadingLogic) : IReadingLogic
    {
        private readonly IProbeRepository _probeRepo = probeRepository;
        private readonly ITemperatureReadingLogic _temperatureReadingLogic = temperatureReadingLogic;
        private readonly ITdsReadingLogic _tdsReadingLogic = tdsReadingLogic;
        private readonly IDiscordNotifierLogic _discordNotifierLogic = discordNotifierLogic;

        public async Task<(bool Success, string? ErrorMessage, Probe Probe, int ColorR, int ColorG, int ColorB)> Insert(PostParams postParams)
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
                await _discordNotifierLogic.SendDiscordMessageAsync($"Une nouvelle sonde a été ajoutée : {machine} avec l'id {newId}");
            }
            else
            {
                // Update RSSI and LastCommunicationDate for existing probe
                await _probeRepo.UpdateCommunicationInfoAsync(probe.Id, postParams.Rssi, DateTime.Now, postParams.FirstLoop ? DateTime.Now : null);
                probe.Rssi = postParams.Rssi;
                probe.LastCommunicationDate = DateTime.Now;
            }

            // Temperature can be empty if the sensor has an issue: skip the insert, the UI will report the error
            double? temperature = null;
            if (!string.IsNullOrWhiteSpace(postParams.Temperature))
            {
                var tempResult = await _temperatureReadingLogic.Insert(postParams, probe);
                if (!tempResult.Success)
                {
                    return (false, tempResult.ErrorMessage, probe, 0, 0, 0);
                }

                temperature = tempResult.Temperature;
            }

            // TDS can also be empty if the sensor has an issue: skip the insert, the UI will report the error
            if (!string.IsNullOrWhiteSpace(postParams.Tds))
            {
                var tdsResult = await _tdsReadingLogic.Insert(postParams, probe);
                if (!tdsResult.Success)
                {
                    return (false, tdsResult.ErrorMessage, probe, 0, 0, 0);
                }
            }

            // Determine the color based on the temperature relative to the probe's range
            // Too cold -> light blue, too hot -> orange, normal -> green
            // No temperature reading -> grey
            var (colorR, colorG, colorB) = temperature.HasValue
                ? GetColorForTemperature(temperature.Value, probe)
                : (128, 128, 128);

            return (true, null, probe, colorR, colorG, colorB);
        }

        private static (int ColorR, int ColorG, int ColorB) GetColorForTemperature(double temperature, Probe probe)
        {
            if (temperature < probe.MinTemperature)
            {
                return (0, 0, 255); // light blue (too cold)
            }

            if (temperature > probe.MaxTemperature)
            {
                return (255, 0, 0); // red (too hot)
            }

            return (0, 128, 0); // green (normal)
        }
    }
}
