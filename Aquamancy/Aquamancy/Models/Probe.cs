namespace Aquamancy.Models
{
    public enum SignalQuality
    {
        Unknown,
        Excellent,
        Good,
        Fair,
        Weak,
        VeryPoor
    }

    public class Probe
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string MachineName { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public double MinTemperature { get; set; }
        public double MaxTemperature { get; set; }
        public int SendFrequencyInSeconds { get; set; } = 60;
        public int TendencySpanHours { get; set; } = 2;
        public double MinimumTendencyChange { get; set; } = 0.3;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastNotifiedAt { get; set; }
        public DateTime? LastCommunicationDate { get; set; }
        public DateTime? LastBootedAt { get; set; }
        public int Rssi { get; set; }

        public SignalQuality RssiQuality => Rssi switch
        {
            0 => SignalQuality.Unknown,
            >= -50 => SignalQuality.Excellent,
            >= -60 => SignalQuality.Good,
            >= -70 => SignalQuality.Fair,
            >= -80 => SignalQuality.Weak,
            _ => SignalQuality.VeryPoor
        };

        public string LastCommunicationAgoDisplay => LastCommunicationDate.HasValue
        ? ((int)(DateTime.Now - LastCommunicationDate.Value).TotalMinutes) switch
        {
            < 1 => "1 min",
            <= 120 => $"{(int)(DateTime.Now - LastCommunicationDate.Value).TotalMinutes} min",
            <= 2880 => $"{(int)(DateTime.Now - LastCommunicationDate.Value).TotalHours} heures",
            _ => $"{(int)(DateTime.Now - LastCommunicationDate.Value).TotalDays} jours"
        }
        : "?";
    }
}
