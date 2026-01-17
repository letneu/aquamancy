using Aquamancy.ILogic;
using System.Text;
using System.Text.Json;

namespace Aquamancy.Logic
{
    public class DiscordNotifierLogic(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<DiscordNotifierLogic> logger, IErrorTriggerLogic errorTriggerLogic) : IDiscordNotifierLogic
    {
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly string _webhookUrl = configuration?.GetValue<string>("Discord:WebhookUrl") ?? string.Empty;
        private readonly bool _notificationEnabled = configuration?.GetValue<bool>("Discord:NotificationEnabled") ?? false;
        private readonly ILogger<DiscordNotifierLogic> _logger = logger;
        private readonly IErrorTriggerLogic _errorTriggerLogic = errorTriggerLogic;

        public async Task SendDiscordMessageAsync(string message)
        {
            if (!_notificationEnabled || string.IsNullOrEmpty(_webhookUrl))
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
            var result = await client.PostAsync(_webhookUrl, json);

            if (!result.IsSuccessStatusCode)
            {
                var reason = await result.Content.ReadAsStringAsync();
                var errorMessage = $"Failed to send Discord message. Status code: {result.StatusCode} with reason : {reason}";
                _errorTriggerLogic.TriggerError(new Exception(reason), errorMessage);
            }
        }
    }
}
