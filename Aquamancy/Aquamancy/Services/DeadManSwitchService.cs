using Aquamancy.Logic;

namespace Aquamancy.Services
{
    public class DeadManSwitchService : BackgroundService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private IServiceScopeFactory _scopeFactory;
        private ILogger<DeadManSwitchService> _logger;
        private readonly string _healthCheckUrl;
        private bool _deadManSwitchEnabled;
        bool _isAlert;
        private static string errorMessage = "@everyone **ALERTE CRITIQUE** -> Impossible de joindre le service externe sur le cloud Azure lors du contrôle de santé, il ne sera pas possible d'être prévenu en cas de perte de courant/réseau ! Vérifiez l'état de santé de AquamancyHealthCheck sur le cloud Azure. Vous pouvez désactiver cette alerte dans le fichier appsettings.";

        public DeadManSwitchService(IHttpClientFactory httpClientFactory, IConfiguration configuration, IServiceScopeFactory scopeFactory, ILogger<DeadManSwitchService> logger)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _healthCheckUrl = configuration["DeadManSwitch:HealthCheckUrl"];
            _deadManSwitchEnabled = configuration.GetValue<bool>("DeadManSwitch:DeadManSwitchEnabled");
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_deadManSwitchEnabled)
            {
                _logger.LogWarning("Disabling DeadManSwitchService based on the appsettings parameter");
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {

                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var discordNotifierLogic = scope.ServiceProvider.GetRequiredService<IDiscordNotifierLogic>();
                    var httpClient = _httpClientFactory.CreateClient();
                    using var response = await httpClient.GetAsync(_healthCheckUrl, stoppingToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        await HandleError(discordNotifierLogic);
                    }

                    if(_isAlert)
                    {
                        await discordNotifierLogic.SendDiscordMessageAsync("Fin de l'alerte, le service externe sur le cloud Azure est de nouveau joignable");
                        _isAlert = false;
                    }

                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Graceful shutdown
                    break;
                }
                catch (Exception ex)
                {
                    await HandleError();
                }
            }
        }

        private async Task HandleError(IDiscordNotifierLogic? discordNotifierLogic = null)
        {
            if (!_isAlert)
            {
                _isAlert = true;
                if(discordNotifierLogic != null)
                {
                    await discordNotifierLogic.SendDiscordMessageAsync(errorMessage);
                }

                _logger.LogError(errorMessage);
            }
        }
    }
}
