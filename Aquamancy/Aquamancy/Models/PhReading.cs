namespace Aquamancy.Models
{
    public class PhReading : IReading
    {
        public int Id { get; set; }
        public int ProbeId { get; set; }
        public DateTime Timestamp { get; set; }
        public double Ph { get; set; }
    }
}
