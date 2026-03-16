using System.Collections.Generic;

namespace Latog_Final_project.Models
{
    public class ChartDataPoint
    {
        public string Label { get; set; } = string.Empty;
        public decimal Value { get; set; }
    }

    public class TopPerformingProduct
    {
        public string ProductName { get; set; } = string.Empty;
        public int TotalSold { get; set; }
        public decimal RevenueGenerated { get; set; }
        public int Progress { get; set; }
    }
}
