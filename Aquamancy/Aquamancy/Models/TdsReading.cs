namespace Aquamancy.Models
{
    public class TdsReading : IReading
    {
        public int Id { get; set; }
        public int ProbeId { get; set; }
        public DateTime Timestamp { get; set; }
        public double Tds { get; set; }
    }
}
