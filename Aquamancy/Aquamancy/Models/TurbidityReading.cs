namespace Aquamancy.Models
{
    public class TurbidityReading : IReading
    {
        public int Id { get; set; }
        public int ProbeId { get; set; }
        public DateTime Timestamp { get; set; }
        public double Turbidity { get; set; }
    }
}
