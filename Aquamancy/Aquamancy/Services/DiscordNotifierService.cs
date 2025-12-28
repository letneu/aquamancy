using Aquamancy.Data;
using Aquamancy.Logic;

namespace Aquamancy.Services
{
    public class DiscordNotifierService : BackgroundService
    {
        private IServiceScopeFactory _scopeFactory;
        private readonly IDiscordNotifierLogic _discordNotifier;
        private bool _isError;
        private int _alertFrequencyInHours;

        public DiscordNotifierService(IServiceScopeFactory scopeFactory, IDiscordNotifierLogic discordNotifier, IConfiguration configuration)
        {
            _scopeFactory = scopeFactory;
            _discordNotifier = discordNotifier;
            _alertFrequencyInHours = configuration.GetValue<int>("Discord:AlertFrequencyInHours");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var probeRepo = scope.ServiceProvider.GetRequiredService<IProbeRepository>();
                    var tempRepo = scope.ServiceProvider.GetRequiredService<ITemperatureRepository>();

                    var probes = await probeRepo.GetAllAsync();

                    foreach (var probe in probes)
                    {
                        var latestTemp = (await tempRepo.GetForProbeAsync(probe.Id, DateTime.Now.AddHours(-1))).FirstOrDefault();

                        if (latestTemp != null && (latestTemp.Temperature < probe.MinTemperature || latestTemp.Temperature > probe.MaxTemperature))
                        {
                            // Only notify once per [_alertFrequencyInHours] per probe
                            var canNotify = !probe.LastNotifiedAt.HasValue || probe.LastNotifiedAt.Value < DateTime.Now.AddHours(-_alertFrequencyInHours);

                            if (canNotify)
                            {
                                await _discordNotifier.SendDiscordMessageAsync($"@everyone ALERTE -> La sonde {probe.Name} a remonté une temperature trop {(latestTemp.Temperature < probe.MinTemperature ? "froide" : "chaude")}" +
                                    $" : {latestTemp.Temperature} C° (seuil entre {probe.MinTemperature} et {probe.MaxTemperature})");

                                await probeRepo.UpdateLastNotifiedAsync(probe.Id, DateTime.Now);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (!_isError)
                    {
                        await _discordNotifier.SendDiscordMessageAsync("Erreur dans le service de notification Discord : " + ex.Message);
                        _isError = true;
                    }
                }

                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
            }
        }
    }
}
