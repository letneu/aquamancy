using Aquamancy.Logic;

namespace Aquamancy.Services
{
    public class DeadManSwitchService : BackgroundService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IDiscordNotifierLogic _discordNotifier;
        private readonly string _healthCheckUrl;
        private bool _deadManSwitchEnabled;
        bool _isAlert;
        private static string errorMessage = "@everyone **ALERTE CRITIQUE** -> Impossible de joindre le service externe sur le cloud Azure lors du contrôle de santé, il ne sera pas possible d'être prévenu en cas de perte de courant/réseau ! Vérifiez l'état de santé de AquamancyHealthCheck sur le cloud Azure.";

        public DeadManSwitchService(IHttpClientFactory httpClientFactory, IDiscordNotifierLogic discordNotifier, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _discordNotifier = discordNotifier;
            _healthCheckUrl = configuration["DeadManSwitch:HealthCheckUrl"];
            _deadManSwitchEnabled = configuration.GetValue<bool>("DeadManSwitch:DeadManSwitchEnabled");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_deadManSwitchEnabled)
            {
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {

                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

                try
                {
                    var httpClient = _httpClientFactory.CreateClient();
                    using var response = await httpClient.GetAsync(_healthCheckUrl, stoppingToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        await HandleError();
                    }

                    if(_isAlert)
                    {
                        await _discordNotifier.SendDiscordMessageAsync("Fin de l'alerte, le service externe sur le cloud Azure est de nouveau joignable");
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

        private async Task HandleError()
        {
            if (!_isAlert)
            {
                _isAlert = true;
                await _discordNotifier.SendDiscordMessageAsync(errorMessage);
            }
        }
    }
}
