using Microsoft.AspNetCore.Mvc.RazorPages;
using Aquamancy.Models;
using Aquamancy.Dto;
using Aquamancy.ILogic;
using Aquamancy.IData;

namespace Aquamancy.Pages
{
    public class IndexModel(IProbeRepository probeRepo, ITemperatureRepository tempRepo, ITurbidityRepository turbidityRepo, IConfiguration configuration, IErrorTriggerLogic errorTriggerLogic) : PageModel
    {
        public List<(Probe Probe, TemperatureReading? TemperatureReading, TurbidityReading? TurbidityReading, double Tendency)> TableInformations { get; set; } = [];

        public ChartDto TemperatureChart { get; set; } = new ChartDto();
        public ChartDto TurbidityChart { get; set; } = new ChartDto();

        private readonly IProbeRepository _probeRepo = probeRepo;
        private readonly ITemperatureRepository _tempRepo = tempRepo;
        private readonly ITurbidityRepository _turbidityRepo = turbidityRepo;
        public readonly IErrorTriggerLogic ErrorTriggerLogic = errorTriggerLogic;

        private readonly int _displayLastHours = configuration.GetValue<int>("Chart:DisplayLastHours");
        public int _fontSizeMultiplier = configuration.GetValue<int>("Chart:FontSizeMultiplier");
        public int _pageRefreshIntervalInMiliSeconds = configuration.GetValue<int>("Chart:RefreshIntervalInSeconds") * 1000;


        public string hotColor = "#c85e5e";
        public string coldColor = "#a1ccf4";

        public async Task OnGetAsync()
        {
            TemperatureChart = new ChartDto();
            TurbidityChart = new ChartDto();

            int n = _displayLastHours;

            // Generate real hourly labels for the last 'n' hours
            var startTime = DateTime.Now.AddHours(-n + 1);

            // Align to the start of the hour
            var startHour = new DateTime(startTime.Year, startTime.Month, startTime.Day, startTime.Hour, 0, 0);

            var labels = Enumerable.Range(0, n).Select(i => startHour.AddHours(i)).ToArray();
            TemperatureChart.labels = [.. labels];
            TurbidityChart.labels = [.. labels];

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
                var turbidityReadings = (await _turbidityRepo.GetForProbeAsync(probe.Id, DateTime.Now.AddHours(-n))).ToList();

                // Create temperature dataset
                var temperatureDataset = CreateDataset(
                    probe,
                    temperatureReadings.Select(r => (r.Timestamp, r.Temperature, IsInRange: r.Temperature >= probe.MinTemperature && r.Temperature <= probe.MaxTemperature))
                );
                TemperatureChart.datasets.Add(temperatureDataset);

                // Create turbidity dataset
                var turbidityDataset = CreateDataset(
                    probe,
                    turbidityReadings.Select(r => (r.Timestamp, r.Turbidity, IsInRange: true)) // Assuming turbidity doesn't have min/max range yet
                );
                TurbidityChart.datasets.Add(turbidityDataset);

                // Only get the latest based on send frequency
                var latestReading = temperatureReadings.OrderByDescending(r => r.Timestamp).Where(r => r.Timestamp >= DateTime.Now.AddSeconds(-probe.SendFrequencyInSeconds - 120)).FirstOrDefault();
                var latestTurbidityReading = turbidityReadings.OrderByDescending(r => r.Timestamp).Where(r => r.Timestamp >= DateTime.Now.AddSeconds(-probe.SendFrequencyInSeconds - 120)).FirstOrDefault();

                var earliestBound = -probe.TendencySpanHours / 2;
                var oldestBound = -probe.TendencySpanHours;
                var recentMean = temperatureReadings?.Where(r => r.Timestamp > DateTime.Now.AddHours(earliestBound))?.Select(r => r.Temperature)?.DefaultIfEmpty(0.0).Average() ?? 0;
                var olderMean = temperatureReadings?.Where(r => r.Timestamp > DateTime.Now.AddHours(oldestBound) && r.Timestamp < DateTime.Now.AddHours(earliestBound))?.Select(r => r.Temperature)?.DefaultIfEmpty(0.0).Average() ?? 0;

                // Don't calculate if we don't have enough data
                var tendency = recentMean != 0 && olderMean != 0 ? recentMean - olderMean : 0;

                TableInformations.Add((probe, latestReading, latestTurbidityReading, tendency));
            }
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

            foreach (var reading in readings)
            {
                var data = new ChartDto.DatasetDto.VectorDto
                {
                    x = reading.Timestamp,
                    y = Convert.ToDouble(reading.Value)
                };

                dataset.data.Add(data);

                var pointBackgroundColor = reading.IsInRange ? probe.Color :
                            Convert.ToDouble(reading.Value) < probe.MinTemperature ? coldColor + "20" :
                            hotColor + "20";

                dataset.pointBackgroundColor.Add(pointBackgroundColor);
                dataset.pointBorderColor.Add(pointBackgroundColor);
                dataset.pointStyle.Add("circle");

                var pointRadius = reading.IsInRange ? 2 : 12;
                dataset.pointRadius.Add(pointRadius);
                dataset.pointHoverRadius.Add(pointRadius + 5);
            }

            return dataset;
        }
    }
}

