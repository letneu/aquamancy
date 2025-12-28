namespace Aquamancy.Models
{
    public class Probe
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string MachineName { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public double MinTemperature { get; set; }
        public double MaxTemperature { get; set; }
        // new property to control how often the probe sends readings (in seconds)
        public int SendFrequencyInSeconds { get; set; } = 60;
        // new property that defines the span in hours used for tendency calculations
        public int TendencySpanHours { get; set; } = 2;
        // minimal change in tendency required to consider it notable
        public double MinimumTendencyChange { get; set; } = 0.3;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastNotifiedAt { get; set; }
    }
}
