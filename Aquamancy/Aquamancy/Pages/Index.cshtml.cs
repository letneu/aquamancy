using Microsoft.AspNetCore.Mvc.RazorPages;
using Aquamancy.Models;
using Aquamancy.Dto;
using Aquamancy.ILogic;
using Aquamancy.IData;

namespace Aquamancy.Pages
{
    public class IndexModel(IProbeRepository probeRepo, ITemperatureRepository tempRepo, IConfiguration configuration, IErrorTriggerLogic errorTriggerLogic) : PageModel
    {
        public List<(Probe Probe, TemperatureReading? TemperatureReading, double Tendency)> TableInformations { get; set; } = [];

        public ChartDto Chart { get; set; } = new ChartDto();

        private readonly IProbeRepository _probeRepo = probeRepo;
        private readonly ITemperatureRepository _tempRepo = tempRepo;
        public readonly IErrorTriggerLogic ErrorTriggerLogic = errorTriggerLogic;

        private readonly int _displayLastHours = configuration.GetValue<int>("Chart:DisplayLastHours");
        public int _fontSizeMultiplier = configuration.GetValue<int>("Chart:FontSizeMultiplier");
        public int _pageRefreshIntervalInMiliSeconds = configuration.GetValue<int>("Chart:RefreshIntervalInSeconds") * 1000;
        public int _minDisplayTemperature = configuration.GetValue<int>("Chart:MinDisplayTemperature");
        public int _maxDisplayTemperature = configuration.GetValue<int>("Chart:MaxDisplayTemperature");

        public async Task OnGetAsync()
        {
            Chart = new ChartDto();

            int n = _displayLastHours;

            // Generate real hourly labels for the last 'n' hours
            var startTime = DateTime.Now.AddHours(-n + 1);

            // Align to the start of the hour
            var startHour = new DateTime(startTime.Year, startTime.Month, startTime.Day, startTime.Hour, 0, 0);

            Chart.labels = [.. (Enumerable.Range(0, n)
                .Select(i => startHour.AddHours(i)))];

            // Load probes from repository
            var probes = (await _probeRepo.GetAllAsync()).ToArray();

            if (probes.Length == 0)
            {
                return;
            }

            foreach(var probe in probes)
            {
                // Get recent readings for this probe
                var readings = (await _tempRepo.GetForProbeAsync(probe.Id, DateTime.Now.AddHours(-n))).ToList();

                ChartDto.DatasetDto dataset = new()
                {
                    label = probe.Name,
                    backgroundColor = probe.Color,
                    borderColor = probe.Color
                };

                foreach (var reading in readings)
                {
                    var data = new ChartDto.DatasetDto.VectorDto
                    {
                        x = reading.Timestamp,
                        y = reading.Temperature
                    };

                    dataset.data.Add(data);

                    var pointBackgroundColor = reading.Temperature >= probe.MinTemperature && reading.Temperature <= probe.MaxTemperature ? probe.Color :
                                reading.Temperature < probe.MinTemperature ? "#007bff" :
                                "#dc3545";

                    dataset.pointBackgroundColor.Add(pointBackgroundColor);
                    dataset.pointBorderColor.Add(pointBackgroundColor);
                    dataset.pointStyle.Add("circle");

                    var pointRadius = reading.Temperature >= probe.MinTemperature && reading.Temperature <= probe.MaxTemperature ? 2 : 6;
                    dataset.pointRadius.Add(pointRadius);
                    dataset.pointHoverRadius.Add(pointRadius + 5);
                }

                Chart.datasets.Add(dataset);

                // Only get the latestest based on send frequency
                var latestReading = readings.OrderByDescending(r => r.Timestamp).Where(r => r.Timestamp >= DateTime.Now.AddSeconds(-probe.SendFrequencyInSeconds - 60)).FirstOrDefault();

                var earliestBound = -probe.TendencySpanHours / 2;
                var oldestBound = -probe.TendencySpanHours;
                var recentMean = readings?.Where(r => r.Timestamp > DateTime.Now.AddHours(earliestBound))?.Select(r => r.Temperature)?.DefaultIfEmpty(0.0).Average() ?? 0;
                var olderMean = readings?.Where(r => r.Timestamp > DateTime.Now.AddHours(oldestBound) && r.Timestamp < DateTime.Now.AddHours(earliestBound))?.Select(r => r.Temperature)?.DefaultIfEmpty(0.0).Average() ?? 0;

                // Don't calculate if we don't have enough data
                var tendency = recentMean != 0 && olderMean != 0 ? recentMean - olderMean : 0;


                TableInformations.Add((probe, latestReading, tendency));
            }
        }
    }
}

