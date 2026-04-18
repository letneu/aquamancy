namespace Aquamancy.Models
{
    public interface IReading
    {
        public int Id { get; set; }
        public int ProbeId { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
