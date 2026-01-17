using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace AquamancyHealthCheck
{
    public class HealthCheckService : BackgroundService
    {
        public static DateTime LastHealthCheckTime;
        private IHttpClientFactory _httpClientFactory;
        private bool _isAlert;
        private readonly string _webhookUrl;
        private readonly int _checkFrequency;


        public HealthCheckService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            LastHealthCheckTime = DateTime.UtcNow;
            _httpClientFactory = httpClientFactory;
            _webhookUrl = configuration["Discord:WebhookUrl"] ?? string.Empty;
            _checkFrequency = configuration.GetValue<int?>("Discord:CheckFrequency") ?? 60;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if(LastHealthCheckTime < DateTime.UtcNow.AddMinutes(-_checkFrequency))
                {
                    if (!_isAlert)
                    {
                        var httpClient = _httpClientFactory.CreateClient();
                        _isAlert = true;
                        await SendDiscordMessage($"@everyone **ALERTE CRITIQUE** ->  Le serveur local aquamancy n'a pas envoyé de signe de vie (ping) au cloud Azure depuis plus de {_checkFrequency} min, perte de courant/réseau détéctée ! Vérifiez l'état du raspberry pi", httpClient);
                    }
                }
                else if (_isAlert)
                {
                    _isAlert = false;
                    var httpClient = _httpClientFactory.CreateClient();
                    await SendDiscordMessage($"@everyone Fin de l'alerte, la connexion a été rétablie", httpClient);
                }
                
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async Task SendDiscordMessage(string message, HttpClient httpClient)
        {
            if (string.IsNullOrWhiteSpace(_webhookUrl))
            {
                // No webhook configured
                return;
            }

            var payload = new
            {
                content = message
            };

            var json = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json"
            );

            await httpClient.PostAsync(_webhookUrl, json);
        }
    }
}
