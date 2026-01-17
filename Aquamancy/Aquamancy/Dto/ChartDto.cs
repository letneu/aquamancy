namespace Aquamancy.Dto
{
    public class ChartDto
    {
        public ChartDto()
        {
            labels = new();
            datasets = new();

        }
        public List<DateTime> labels { get; set; }
        public List<DatasetDto> datasets { get; set; }

        public class DatasetDto
        {
            public DatasetDto()
            {
                data = new();
                pointBackgroundColor = new();
                pointBorderColor = new();
                pointStyle = new();
                pointRadius = new();
                pointHoverRadius = new();
            }

            public string? label { get; set; }
            public string? backgroundColor { get; set; }
            public string? borderColor { get; set; }
            public bool fill { get; set; }
            public List<VectorDto> data { get; set; }
            public List<string> pointBackgroundColor { get; set; }
            public List<string> pointBorderColor { get; set; }
            public List<string> pointStyle { get; set; }
            public List<int> pointRadius { get; set; }
            public List<int> pointHoverRadius { get; set; }

            public class VectorDto
            {
                public DateTime x { get; set; }
                public double y { get; set; }
            }

        }

    }
}
