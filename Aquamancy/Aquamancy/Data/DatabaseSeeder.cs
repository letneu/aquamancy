using Aquamancy.IData;
using Aquamancy.Models;

namespace Aquamancy.Data
{
    public class DatabaseSeeder
    {
        private readonly IProbeRepository _probeRepo;
        private readonly ITemperatureRepository _temperatureRepo;
        private readonly ITurbidityRepository _turbidityRepo;

        public DatabaseSeeder(IProbeRepository probeRepo, ITemperatureRepository temperatureRepo, ITurbidityRepository turbidityRepo)
        {
            _probeRepo = probeRepo;
            _temperatureRepo = temperatureRepo;
            _turbidityRepo = turbidityRepo;
        }

        public async Task SeedTestDataAsync()
        {
            // Check if probes already exist
            var existingProbes = (await _probeRepo.GetAllAsync()).ToList();

            // Only seed if no probes exist
            if (existingProbes.Count > 0)
            {
                Console.WriteLine("Données de test déjà présentes, ajout de nouvelles mesures...");
                await AddNewReadingsAsync(existingProbes);
                return;
            }

            Console.WriteLine("Création de données de test...");

            // Create test probes
            var probe1 = new Probe
            {
                Name = "Aquarium Principal",
                MachineName = "probe-1",
                Color = "#3498db",
                MinTemperature = 24.0,
                MaxTemperature = 26.0,
                SendFrequencyInSeconds = 300,
                TendencySpanHours = 2,
                MinimumTendencyChange = 0.3,
                CreatedAt = DateTime.Now.AddDays(-30),
                LastCommunicationDate = DateTime.Now.AddMinutes(-5),
                LastBootedAt = DateTime.Now.AddDays(-7),
                Rssi = -65
            };

            var probe2 = new Probe
            {
                Name = "Aquarium Récifal",
                MachineName = "probe-2",
                Color = "#e74c3c",
                MinTemperature = 25.0,
                MaxTemperature = 27.0,
                SendFrequencyInSeconds = 300,
                TendencySpanHours = 2,
                MinimumTendencyChange = 0.3,
                CreatedAt = DateTime.Now.AddDays(-30),
                LastCommunicationDate = DateTime.Now.AddMinutes(-3),
                LastBootedAt = DateTime.Now.AddDays(-5),
                Rssi = -55
            };

            var probe3 = new Probe
            {
                Name = "Bassin Quarantaine",
                MachineName = "probe-3",
                Color = "#2ecc71",
                MinTemperature = 23.0,
                MaxTemperature = 25.0,
                SendFrequencyInSeconds = 300,
                TendencySpanHours = 2,
                MinimumTendencyChange = 0.3,
                CreatedAt = DateTime.Now.AddDays(-15),
                LastCommunicationDate = DateTime.Now.AddMinutes(-10),
                LastBootedAt = DateTime.Now.AddDays(-3),
                Rssi = -72
            };

            probe1.Id = await _probeRepo.AddAsync(probe1);
            probe2.Id = await _probeRepo.AddAsync(probe2);
            probe3.Id = await _probeRepo.AddAsync(probe3);

            existingProbes = new List<Probe> { probe1, probe2, probe3 };

            // Generate readings for the last 24 hours
            await GenerateReadingsAsync(existingProbes, 24);

            Console.WriteLine($"Données de test créées : {existingProbes.Count} sondes avec des mesures sur 24h");
        }

        private async Task AddNewReadingsAsync(List<Probe> probes)
        {
            // Add readings just for the last hour
            await GenerateReadingsAsync(probes, 1);
            Console.WriteLine($"Nouvelles mesures ajoutées pour {probes.Count} sondes");
        }

        private async Task GenerateReadingsAsync(List<Probe> probes, int hoursBack)
        {
            var random = new Random();
            var now = DateTime.Now;

            foreach (var probe in probes)
            {
                // Base temperature and turbidity for this probe
                var baseTemp = (probe.MinTemperature + probe.MaxTemperature) / 2;
                var baseTurbidity = 1.5 + random.NextDouble() * 2.0; // entre 1.5 et 3.5 NTU

                // Generate readings every 5 minutes for the specified hours
                var totalMinutes = hoursBack * 60;
                for (int i = 0; i < totalMinutes; i += 5)
                {
                    var timestamp = now.AddMinutes(-totalMinutes + i);

                    // Temperature variation (sine wave with some randomness)
                    var hourOfDay = timestamp.Hour + timestamp.Minute / 60.0;
                    var dailyCycle = Math.Sin((hourOfDay - 6) * Math.PI / 12) * 0.5; // Variation de ±0.5°C selon l'heure
                    var randomVariation = (random.NextDouble() - 0.5) * 0.4; // ±0.2°C random
                    var temperature = baseTemp + dailyCycle + randomVariation;

                    // Occasionally go out of range
                    if (random.NextDouble() < 0.05) // 5% chance
                    {
                        temperature += random.NextDouble() < 0.5 ? -1.5 : 1.5;
                    }

                    // Turbidity variation (mostly stable with occasional spikes)
                    var turbidity = baseTurbidity + (random.NextDouble() - 0.5) * 0.3;

                    // Occasional turbidity spike (algae bloom, feeding, etc.)
                    if (random.NextDouble() < 0.03) // 3% chance
                    {
                        turbidity += random.NextDouble() * 3.0;
                    }

                    // Add temperature reading
                    await _temperatureRepo.AddAsync(new TemperatureReading
                    {
                        ProbeId = probe.Id,
                        Timestamp = timestamp,
                        Temperature = Math.Round(temperature, 2)
                    });

                    // Add turbidity reading
                    await _turbidityRepo.AddAsync(new TurbidityReading
                    {
                        ProbeId = probe.Id,
                        Timestamp = timestamp,
                        Turbidity = Math.Round(turbidity, 2)
                    });
                }
            }
        }
    }
}
