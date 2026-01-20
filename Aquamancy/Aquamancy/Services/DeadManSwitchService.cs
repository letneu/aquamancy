using Aquamancy.ILogic;

namespace Aquamancy.Services
{
    public class DeadManSwitchService(IHttpClientFactory httpClientFactory, IConfiguration configuration, IServiceScopeFactory scopeFactory, ILogger<DeadManSwitchService> logger, IErrorTriggerLogic errorTriggerLogic) : BackgroundService
    {
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly IErrorTriggerLogic _errorTriggerLogic = errorTriggerLogic;
        private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
        private readonly ILogger<DeadManSwitchService> _logger = logger;

        private string _healthCheckUrl = configuration?["DeadManSwitch:HealthCheckUrl"] ?? string.Empty;
        private readonly bool _deadManSwitchEnabled = configuration?.GetValue<bool>("DeadManSwitch:DeadManSwitchEnabled") ?? false;
        private readonly int _errorThreshold = configuration?.GetValue<int>("DeadManSwitch:ErrorThreshold") ?? 12;
        private readonly int _checkIntervalMinutes = configuration?.GetValue<int>("DeadManSwitch:CheckIntervalMinutes") ?? 5;

        bool _isAlert;
        int _errorCount = 0;
        private static readonly string errorMessage = "@everyone **ALERTE CRITIQUE** -> Impossible de joindre le service externe sur le cloud Azure lors du contrôle de santé, il ne sera pas possible d'être prévenu en cas de perte de courant/réseau ! Vérifiez l'état de santé de AquamancyHealthCheck sur le cloud Azure. Vous pouvez désactiver cette alerte dans le fichier appsettings.";

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_deadManSwitchEnabled)
            {
                _logger.LogWarning("Disabling DeadManSwitchService based on the appsettings parameter");
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _scopeFactory.CreateScope();
                var discordNotifierLogic = scope.ServiceProvider.GetRequiredService<IDiscordNotifierLogic>();

                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(_checkIntervalMinutes), stoppingToken);
                    var httpClient = _httpClientFactory.CreateClient();
                    using var response = await httpClient.GetAsync(_healthCheckUrl, stoppingToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new HttpRequestException($"{response.StatusCode} {await response.Content.ReadAsStreamAsync(stoppingToken)}");
                    }

                    // From here, the health check was successful else we would have throwed

                    // If we were in alert state, send a recovery message
                    if (_isAlert)
                    {
                        await discordNotifierLogic.SendDiscordMessageAsync("Fin de l'alerte, le service externe sur le cloud Azure est de nouveau joignable");
                        _isAlert = false;
                    }
                    
                    _errorCount = 0;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Graceful shutdown
                    break;
                }
                catch (Exception ex)
                {
                    _errorCount++;

                    if (_errorCount >= _errorThreshold)
                    {
                        if (!_isAlert)
                        {
                            await discordNotifierLogic.SendDiscordMessageAsync(errorMessage);
                            _isAlert = true;
                        }

                        _errorTriggerLogic.TriggerError(ex, errorMessage);
                    }
                    else
                    {
                        _logger.LogWarning("Erreur lors du contrôle de santé ({ErrorCount}/{ErrorThreshold}): {Message}", _errorCount, _errorThreshold, ex.Message);
                    }
                }
            }
        }
    }
}
