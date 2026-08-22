namespace Aquamancy.Dto
{
    public class AppSettingsDto
    {
        // Chart
        public int DisplayLastHours { get; set; }
        public int FontSizeMultiplier { get; set; }
        public int RefreshIntervalInSeconds { get; set; }
        public int MinDisplayTemperature { get; set; }
        public int MaxDisplayTemperature { get; set; }

        // Discord
        public bool NotificationEnabled { get; set; }
        public string WebhookUrl { get; set; } = string.Empty;
        public int AlertFrequencyInHours { get; set; }
        public int DiscordCheckIntervalMinutes { get; set; }

        // DeadManSwitch
        public bool DeadManSwitchEnabled { get; set; }
        public string HealthCheckUrl { get; set; } = string.Empty;
        public int ErrorThreshold { get; set; }
        public int DeadManSwitchCheckIntervalMinutes { get; set; }

        // ErrorOnGui
        public bool ErrorOnGuiEnabled { get; set; }
        public int TimeBeforeClearingErrorsInMinutes { get; set; }
    }
}
