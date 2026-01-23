using Aquamancy.IData;
using Aquamancy.ILogic;
using Microsoft.Extensions.Logging;

namespace Aquamancy.Services
{
    public class DiscordNotifierService(IServiceScopeFactory scopeFactory, IConfiguration configuration, IErrorTriggerLogic errorTriggerLogic, ILogger<DiscordNotifierService> logger) : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
        private readonly int _alertFrequencyInHours = configuration?.GetValue<int>("Discord:AlertFrequencyInHours") ?? 8;
        private readonly bool _discordNotifierEnabled = configuration?.GetValue<bool>("Discord:NotificationEnabled") ?? false;
        private readonly int _checkIntervalMinutes = configuration?.GetValue<int>("Discord:CheckIntervalMinutes") ?? 10;
        private readonly IErrorTriggerLogic _errorTriggerLogic = errorTriggerLogic;
        private readonly ILogger<DiscordNotifierService> _logger = logger;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_discordNotifierEnabled)
            {
                _logger.LogWarning("Disabling DiscordNotifierService based on the appsettings parameter");
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Wait first so we have time to receive data from probes after a restart
                    await Task.Delay(TimeSpan.FromMinutes(_checkIntervalMinutes), stoppingToken);

                    using var scope = _scopeFactory.CreateScope();
                    var probeRepo = scope.ServiceProvider.GetRequiredService<IProbeRepository>();
                    var tempRepo = scope.ServiceProvider.GetRequiredService<ITemperatureRepository>();
                    var discordNotifier = scope.ServiceProvider.GetRequiredService<IDiscordNotifierLogic>();

                    var probes = await probeRepo.GetAllAsync();

                    foreach (var probe in probes)
                    {
                        // Determine how far back we should look for the latest temperature
                        var probeRefreshFrequency = DateTime.Now.AddSeconds(-probe.SendFrequencyInSeconds * 2);
                        var defaultLookBack = DateTime.Now.AddHours(-2);
                        var howFarBack = probeRefreshFrequency > defaultLookBack ? probeRefreshFrequency : defaultLookBack;

                        var latestTemp = (await tempRepo.GetForProbeAsync(probe.Id, howFarBack)).FirstOrDefault();

                        // Only notify once per [_alertFrequencyInHours] per probe
                        if (!probe.LastNotifiedAt.HasValue || probe.LastNotifiedAt.Value < DateTime.Now.AddHours(-_alertFrequencyInHours))
                        {
                            // Check if temperature is out of bounds
                            if (latestTemp != null && (latestTemp.Temperature < probe.MinTemperature || latestTemp.Temperature > probe.MaxTemperature))
                            {
                                await discordNotifier.SendDiscordMessageAsync($"@everyone ALERTE -> La sonde {probe.Name} a remonté une temperature trop {(latestTemp.Temperature < probe.MinTemperature ? "froide" : "chaude")}" +
                                        $" : {latestTemp.Temperature} C° (seuil entre {probe.MinTemperature} et {probe.MaxTemperature})");
                                await probeRepo.UpdateLastNotifiedAsync(probe.Id, DateTime.Now);
                            }
                            // Check if no temperature has been received recently
                            else if (latestTemp == null)
                            {
                                await discordNotifier.SendDiscordMessageAsync($"@everyone ALERTE -> La sonde {probe.Name} ne remonte plus de temperature depuis un certain temps, vérifiez les branchement de la sonde, la led ne doit pas clignoter");
                                await probeRepo.UpdateLastNotifiedAsync(probe.Id, DateTime.Now);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _errorTriggerLogic.TriggerError(ex, "Impossible d'envoyer le message Discord, vérifiez la configuration du webhook");
                }
            }
        }
    }
}
