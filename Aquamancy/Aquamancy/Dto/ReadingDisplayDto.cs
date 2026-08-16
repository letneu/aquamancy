namespace Aquamancy.Dto
{
    public class ReadingDisplayDto
    {
        public string Label { get; set; } = "";
        public double? Value { get; set; }
        public string ValueFormat { get; set; } = "F1";
        public string Unit { get; set; } = "";
        public string ValueColor { get; set; } = "";
    }
}
