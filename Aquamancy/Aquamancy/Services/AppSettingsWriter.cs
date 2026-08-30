using System.Globalization;
using System.Text.RegularExpressions;
using Aquamancy.Dto;
using Aquamancy.ILogic;

namespace Aquamancy.Services
{
    /// <summary>
    /// Lit et écrit des valeurs dans appsettings.json en préservant les commentaires et la mise en forme.
    /// L'écriture se fait par remplacement ciblé (regex) de la valeur d'une clé au sein de sa section.
    /// </summary>
    public class AppSettingsWriter(IConfiguration configuration, IWebHostEnvironment environment) : IAppSettingsWriter
    {
        private readonly IConfiguration _configuration = configuration;
        private readonly string _filePath = Path.Combine(environment.ContentRootPath, "appsettings.json");
        private static readonly object _lock = new();

        public AppSettingsDto Read()
        {
            return new AppSettingsDto
            {
                DisplayLastHoursTemperature = _configuration.GetValue<int>("Chart:DisplayLastHoursTemperature"),
                DisplayLastHoursTds = _configuration.GetValue<int>("Chart:DisplayLastHoursTds"),
                FontSizeMultiplier = _configuration.GetValue<int>("Chart:FontSizeMultiplier"),
                RefreshIntervalInSeconds = _configuration.GetValue<int>("Chart:RefreshIntervalInSeconds"),
                NotificationEnabled = _configuration.GetValue<bool>("Discord:NotificationEnabled"),
                WebhookUrl = _configuration.GetValue<string>("Discord:WebhookUrl") ?? string.Empty,
                AlertFrequencyInHours = _configuration.GetValue<int>("Discord:AlertFrequencyInHours"),
                DiscordCheckIntervalMinutes = _configuration.GetValue<int>("Discord:CheckIntervalMinutes"),
                DeadManSwitchEnabled = _configuration.GetValue<bool>("DeadManSwitch:DeadManSwitchEnabled"),
                HealthCheckUrl = _configuration.GetValue<string>("DeadManSwitch:HealthCheckUrl") ?? string.Empty,
                ErrorThreshold = _configuration.GetValue<int>("DeadManSwitch:ErrorThreshold"),
                DeadManSwitchCheckIntervalMinutes = _configuration.GetValue<int>("DeadManSwitch:CheckIntervalMinutes"),
                ErrorOnGuiEnabled = _configuration.GetValue<bool>("ErrorOnGui:ErrorOnGuiEnabled"),
                TimeBeforeClearingErrorsInMinutes = _configuration.GetValue<int>("ErrorOnGui:TimeBeforeClearingErrorsInMinutes")
            };
        }

        public void Write(AppSettingsDto settings)
        {
            lock (_lock)
            {
                var json = File.ReadAllText(_filePath);

                json = ReplaceValue(json, "Chart", "DisplayLastHoursTemperature", settings.DisplayLastHoursTemperature.ToString(CultureInfo.InvariantCulture));
                json = ReplaceValue(json, "Chart", "DisplayLastHoursTds", settings.DisplayLastHoursTds.ToString(CultureInfo.InvariantCulture));
                json = ReplaceValue(json, "Chart", "FontSizeMultiplier", settings.FontSizeMultiplier.ToString(CultureInfo.InvariantCulture));
                json = ReplaceValue(json, "Chart", "RefreshIntervalInSeconds", settings.RefreshIntervalInSeconds.ToString(CultureInfo.InvariantCulture));

                json = ReplaceValue(json, "Discord", "NotificationEnabled", settings.NotificationEnabled ? "true" : "false");
                json = ReplaceValue(json, "Discord", "WebhookUrl", ToJsonString(settings.WebhookUrl));
                json = ReplaceValue(json, "Discord", "AlertFrequencyInHours", settings.AlertFrequencyInHours.ToString(CultureInfo.InvariantCulture));
                json = ReplaceValue(json, "Discord", "CheckIntervalMinutes", settings.DiscordCheckIntervalMinutes.ToString(CultureInfo.InvariantCulture));

                json = ReplaceValue(json, "DeadManSwitch", "DeadManSwitchEnabled", settings.DeadManSwitchEnabled ? "true" : "false");
                json = ReplaceValue(json, "DeadManSwitch", "HealthCheckUrl", ToJsonString(settings.HealthCheckUrl));
                json = ReplaceValue(json, "DeadManSwitch", "ErrorThreshold", settings.ErrorThreshold.ToString(CultureInfo.InvariantCulture));
                json = ReplaceValue(json, "DeadManSwitch", "CheckIntervalMinutes", settings.DeadManSwitchCheckIntervalMinutes.ToString(CultureInfo.InvariantCulture));

                json = ReplaceValue(json, "ErrorOnGui", "ErrorOnGuiEnabled", settings.ErrorOnGuiEnabled ? "true" : "false");
                json = ReplaceValue(json, "ErrorOnGui", "TimeBeforeClearingErrorsInMinutes", settings.TimeBeforeClearingErrorsInMinutes.ToString(CultureInfo.InvariantCulture));

                File.WriteAllText(_filePath, json);
            }
        }

        /// <summary>
        /// Remplace la valeur de "key" à l'intérieur de la section "section", sans toucher aux commentaires.
        /// </summary>
        private static string ReplaceValue(string json, string section, string key, string newValue)
        {
            // Localise le début de la section, puis la clé qui suit (la première occurrence après la section)
            var sectionMatch = Regex.Match(json, $"\"{Regex.Escape(section)}\"\\s*:\\s*{{");
            if (!sectionMatch.Success)
            {
                throw new InvalidOperationException($"Section '{section}' introuvable dans appsettings.json.");
            }

            var keyRegex = new Regex($"(\"{Regex.Escape(key)}\"\\s*:\\s*)(\"(?:[^\"\\\\]|\\\\.)*\"|[^,\\r\\n}}]+)");
            var keyMatch = keyRegex.Match(json, sectionMatch.Index + sectionMatch.Length);
            if (!keyMatch.Success)
            {
                throw new InvalidOperationException($"Clé '{key}' introuvable dans la section '{section}' de appsettings.json.");
            }

            return string.Concat(
                json.AsSpan(0, keyMatch.Index),
                keyMatch.Groups[1].Value + newValue,
                json.AsSpan(keyMatch.Index + keyMatch.Length));
        }

        private static string ToJsonString(string value)
        {
            return System.Text.Json.JsonSerializer.Serialize(value ?? string.Empty);
        }
    }
}
