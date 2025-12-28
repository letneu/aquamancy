using System.Text;
using System.Text.Json;

namespace Aquamancy.Logic
{
    public class DiscordNotifierLogic : IDiscordNotifierLogic
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _webhookUrl;
        private bool _notificationEnabled;
        private ILogger<DiscordNotifierLogic> _logger;

        public DiscordNotifierLogic(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<DiscordNotifierLogic> logger)
        {
            _httpClientFactory = httpClientFactory;
            _webhookUrl = configuration.GetValue<string>("Discord:WebhookUrl");
            _notificationEnabled = configuration.GetValue<bool>("Discord:NotificationEnabled");
            _logger = logger;
        }

        public async Task SendDiscordMessageAsync(string message)
        {
            if(!_notificationEnabled || string.IsNullOrEmpty(_webhookUrl))
            {
                _logger.LogWarning("Can't send Discord message because notifications are disabled in the app settings or the web hook url is empty");
                return;
            }

            _logger.LogInformation("Sending Discord message: {Message}", message);

            var payload = new
            {
                content = message
            };

            var json = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json"
            );

            var client = _httpClientFactory.CreateClient();
            await client.PostAsync(_webhookUrl, json);
        }
    }
}
