using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Aquamancy.Models;
using Aquamancy.Dto;
using Aquamancy.ILogic;
using Aquamancy.IData;

namespace Aquamancy.Pages
{
    public class IndexModel(IProbeRepository probeRepo, ITemperatureRepository tempRepo, ITdsRepository tdsRepo, IConfiguration configuration, IErrorTriggerLogic errorTriggerLogic) : PageModel
    {
        public List<(Probe Probe, TemperatureReading? TemperatureReading, TdsReading? TdsReading)> TableInformations { get; set; } = [];

        public ChartDto TemperatureChart { get; set; } = new ChartDto();
        public ChartDto TdsChart { get; set; } = new ChartDto();

        private readonly IProbeRepository _probeRepo = probeRepo;
        private readonly ITemperatureRepository _tempRepo = tempRepo;
        private readonly ITdsRepository _tdsRepo = tdsRepo;
        public readonly IErrorTriggerLogic ErrorTriggerLogic = errorTriggerLogic;

        private readonly int _displayLastHours = configuration.GetValue<int>("Chart:DisplayLastHours");
        public int _fontSizeMultiplier = configuration.GetValue<int>("Chart:FontSizeMultiplier");
        public int _pageRefreshIntervalInMiliSeconds = configuration.GetValue<int>("Chart:RefreshIntervalInSeconds") * 1000;


        public string hotColor = "#c85e5e";
        public string coldColor = "#a1ccf4";

        public async Task OnGetAsync()
        {
            TemperatureChart = new ChartDto();
            TdsChart = new ChartDto();

            int n = _displayLastHours;

            // Generate real hourly labels for the last 'n' hours
            var startTime = DateTime.Now.AddHours(-n + 1);

            // Align to the start of the hour
            var startHour = new DateTime(startTime.Year, startTime.Month, startTime.Day, startTime.Hour, 0, 0);

            var labels = Enumerable.Range(0, n).Select(i => startHour.AddHours(i)).ToArray();
            TemperatureChart.labels = [.. labels];
            TdsChart.labels = [.. labels];

            // Load probes from repository
            var probes = (await _probeRepo.GetAllAsync()).ToArray();

            if (probes.Length == 0)
            {
                return;
            }

            foreach (var probe in probes)
            {
                // Get recent readings for this probe
                var temperatureReadings = (await _tempRepo.GetForProbeAsync(probe.Id, DateTime.Now.AddHours(-n))).ToList();
                var tdsReadings = (await _tdsRepo.GetForProbeAsync(probe.Id, DateTime.Now.AddHours(-n))).ToList();

                // Create temperature dataset
                var temperatureDataset = CreateDataset(
                    probe,
                    temperatureReadings.Select(r => (r.Timestamp, r.Temperature, IsInRange: r.Temperature >= probe.MinTemperature && r.Temperature <= probe.MaxTemperature))
                );
                TemperatureChart.datasets.Add(temperatureDataset);

                // Create TDS dataset
                var tdsDataset = CreateDataset(
                    probe,
                    tdsReadings.Select(r => (r.Timestamp, r.Tds, IsInRange: true)) // Assuming TDS doesn't have min/max range yet
                );
                TdsChart.datasets.Add(tdsDataset);

                // Only get the latest based on send frequency
                var latestReading = temperatureReadings.OrderByDescending(r => r.Timestamp).Where(r => r.Timestamp >= DateTime.Now.AddSeconds(-probe.SendFrequencyInSeconds - 120)).FirstOrDefault();
                var latestTdsReading = tdsReadings.OrderByDescending(r => r.Timestamp).Where(r => r.Timestamp >= DateTime.Now.AddSeconds(-probe.SendFrequencyInSeconds - 120)).FirstOrDefault();

                TableInformations.Add((probe, latestReading, latestTdsReading));
            }
        }

        public async Task<IActionResult> OnGetProbeAsync(int id)
        {
            var probe = await _probeRepo.GetByIdAsync(id);
            if (probe == null)
            {
                return NotFound();
            }

            return new JsonResult(new
            {
                probe.Id,
                probe.Name,
                probe.MachineName,
                probe.Color,
                probe.MinTemperature,
                probe.MaxTemperature,
                probe.SendFrequencyInSeconds
            });
        }

        public async Task<IActionResult> OnPostUpdateProbeAsync([FromForm] ProbeSettingsDto settings)
        {
            var probe = await _probeRepo.GetByIdAsync(settings.Id);
            if (probe == null)
            {
                return NotFound();
            }

            probe.Name = settings.Name;
            probe.Color = settings.Color ?? string.Empty;
            probe.MinTemperature = Math.Round(ParseInvariantDouble(settings.MinTemperature, probe.MinTemperature));
            probe.MaxTemperature = Math.Round(ParseInvariantDouble(settings.MaxTemperature, probe.MaxTemperature));
            probe.SendFrequencyInSeconds = settings.SendFrequencyInSeconds;

            await _probeRepo.UpdateSettingsAsync(probe);

            return new JsonResult(new { success = true });
        }

        private static double ParseInvariantDouble(string? value, double fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            // Les champs <input type="number"> envoient toujours le séparateur décimal '.',
            // on parse donc en InvariantCulture pour éviter que les valeurs décimales
            // (ex : 24.5) échouent sous une culture utilisant la virgule et retombent à 0.
            var normalized = value.Replace(',', '.');
            if (double.TryParse(normalized, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var result))
            {
                return result;
            }

            return fallback;
        }

        public class ProbeSettingsDto
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string? MachineName { get; set; }
            public string? Color { get; set; }
            public string? MinTemperature { get; set; }
            public string? MaxTemperature { get; set; }
            public int SendFrequencyInSeconds { get; set; }
        }

        private ChartDto.DatasetDto CreateDataset<T>(Probe probe, IEnumerable<(DateTime Timestamp, T Value, bool IsInRange)> readings)
        {
            var dataset = new ChartDto.DatasetDto
            {
                label = probe.Name,
                backgroundColor = probe.Color,
                borderColor = probe.Color,
                tension = 0.4
            };

            // Catégorie de l'état précédent : 0 = dans la plage, -1 = froid, 1 = chaud
            int previousState = 0;
            bool first = true;
            // Nombre de points depuis le dernier marqueur affiché (pour espacer les repères)
            int pointsSinceLastMarker = 0;
            // On répète un repère tous les N points tant que l'épisode dure
            const int markerSpacing = 6;

            foreach (var reading in readings)
            {
                var data = new ChartDto.DatasetDto.VectorDto
                {
                    x = reading.Timestamp,
                    y = Convert.ToDouble(reading.Value)
                };

                dataset.data.Add(data);

                var value = Convert.ToDouble(reading.Value);
                var isCold = !reading.IsInRange && value < probe.MinTemperature;
                var isHot = !reading.IsInRange && value > probe.MaxTemperature;
                var currentState = isHot ? 1 : isCold ? -1 : 0;

                var pointColor = reading.IsInRange ? probe.Color :
                            isCold ? coldColor :
                            hotColor;

                dataset.pointBackgroundColor.Add(pointColor);
                dataset.pointBorderColor.Add(pointColor);

                // Forme évocatrice : flamme (emoji) pour le chaud, flocon (emoji) pour le froid.
                // "flame" et "snow" sont des jetons remplacés par des images d'emoji côté JavaScript.
                var pointStyle = isHot ? "flame" :
                            isCold ? "snow" :
                            "circle";
                dataset.pointStyle.Add(pointStyle);

                // Un marqueur est affiché au DÉBUT d'un épisode (changement d'état),
                // puis répété tous les 'markerSpacing' points tant qu'il dure, afin
                // qu'un repère reste visible même si l'épisode couvre tout le graphique
                pointsSinceLastMarker++;
                var isStreakStart = currentState != 0 && (first || currentState != previousState);
                var showMarker = currentState != 0 && (isStreakStart || pointsSinceLastMarker >= markerSpacing);

                if (showMarker)
                {
                    pointsSinceLastMarker = 0;
                }

                var pointRadius = showMarker ? 7 : 0;
                dataset.pointRadius.Add(pointRadius);
                dataset.pointHoverRadius.Add(reading.IsInRange ? 4 : 11);

                previousState = currentState;
                first = false;
            }

            return dataset;
        }
    }
}

