namespace Aquamancy.Models
{
    public class TemperatureReading
    {
        public int Id { get; set; }
        public int ProbeId { get; set; }
        public DateTime Timestamp { get; set; }
        public double Temperature { get; set; }
    }
}
