using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Aquamancy.Models;
using Aquamancy.Dto;
using Aquamancy.ILogic;
using Aquamancy.IData;

namespace Aquamancy.Pages
{
    public class IndexModel(IProbeRepository probeRepo, ITemperatureRepository tempRepo, ITdsRepository tdsRepo, IConfiguration configuration, IErrorTriggerLogic errorTriggerLogic, IAppSettingsWriter appSettingsWriter) : PageModel
    {
        public List<(Probe Probe, TemperatureReading? TemperatureReading, TdsReading? TdsReading, bool HasTemperatureError, bool HasTdsError)> TableInformations { get; set; } = [];

        public ChartDto TemperatureChart { get; set; } = new ChartDto();
        public ChartDto TdsChart { get; set; } = new ChartDto();

        private readonly IProbeRepository _probeRepo = probeRepo;
        private readonly ITemperatureRepository _tempRepo = tempRepo;
        private readonly ITdsRepository _tdsRepo = tdsRepo;
        public readonly IErrorTriggerLogic ErrorTriggerLogic = errorTriggerLogic;
        private readonly IAppSettingsWriter _appSettingsWriter = appSettingsWriter;

        [BindProperty]
        public AppSettingsDto AppSettings { get; set; } = new AppSettingsDto();

        private readonly int _displayLastHoursTemperature = configuration.GetValue<int>("Chart:DisplayLastHoursTemperature");
        private readonly int _displayLastHoursTds = configuration.GetValue<int>("Chart:DisplayLastHoursTds");
        public int _fontSizeMultiplier = configuration.GetValue<int>("Chart:FontSizeMultiplier");
        public int _pageRefreshIntervalInMiliSeconds = configuration.GetValue<int>("Chart:RefreshIntervalInSeconds") * 1000;


        public string hotColor = "#c85e5e";
        public string coldColor = "#a1ccf4";

        public async Task OnGetAsync()
        {
            AppSettings = _appSettingsWriter.Read();

            TemperatureChart = new ChartDto();
            TdsChart = new ChartDto();

            int nTemperature = _displayLastHoursTemperature;
            int nTds = _displayLastHoursTds;

            // Generate real hourly labels for the last 'n' hours (temperature)
            var startTimeTemperature = DateTime.Now.AddHours(-nTemperature + 1);
            var startHourTemperature = new DateTime(startTimeTemperature.Year, startTimeTemperature.Month, startTimeTemperature.Day, startTimeTemperature.Hour, 0, 0);
            var temperatureLabels = Enumerable.Range(0, nTemperature).Select(i => startHourTemperature.AddHours(i)).ToArray();
            TemperatureChart.labels = [.. temperatureLabels];

            // Generate real hourly labels for the last 'n' hours (TDS)
            var startTimeTds = DateTime.Now.AddHours(-nTds + 1);
            var startHourTds = new DateTime(startTimeTds.Year, startTimeTds.Month, startTimeTds.Day, startTimeTds.Hour, 0, 0);
            var tdsLabels = Enumerable.Range(0, nTds).Select(i => startHourTds.AddHours(i)).ToArray();
            TdsChart.labels = [.. tdsLabels];

            // Load probes from repository
            var probes = (await _probeRepo.GetAllAsync()).ToArray();

            if (probes.Length == 0)
            {
                return;
            }

            foreach (var probe in probes)
            {
                // Get recent readings for this probe
                var temperatureReadings = (await _tempRepo.GetForProbeAsync(probe.Id, DateTime.Now.AddHours(-nTemperature))).ToList();
                var tdsReadings = (await _tdsRepo.GetForProbeAsync(probe.Id, DateTime.Now.AddHours(-nTds))).ToList();

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
                var recentWindowStart = DateTime.Now.AddSeconds(-probe.SendFrequencyInSeconds - 120);
                var latestReading = temperatureReadings.OrderByDescending(r => r.Timestamp).Where(r => r.Timestamp >= recentWindowStart).FirstOrDefault();
                var latestTdsReading = tdsReadings.OrderByDescending(r => r.Timestamp).Where(r => r.Timestamp >= recentWindowStart).FirstOrDefault();

                // La sonde a communiqué récemment mais sans lecture correspondante => capteur en erreur
                var hasRecentCommunication = probe.LastCommunicationDate.HasValue && probe.LastCommunicationDate.Value >= recentWindowStart;
                var hasTemperatureError = hasRecentCommunication && latestReading is null;
                var hasTdsError = probe.TdsEnabled && hasRecentCommunication && latestTdsReading is null;

                TableInformations.Add((probe, latestReading, latestTdsReading, hasTemperatureError, hasTdsError));
            }
        }

        public IActionResult OnPostSaveSettings()
        {
            _appSettingsWriter.Write(AppSettings);
            return RedirectToPage();
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
                probe.SendFrequencyInSeconds,
                probe.TdsEnabled
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
            probe.TdsEnabled = settings.TdsEnabled;

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
            public bool TdsEnabled { get; set; }
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

