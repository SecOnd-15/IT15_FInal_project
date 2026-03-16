namespace Latog_Final_project.Models
{
    public class ProcurementReportViewModel
    {
        public int TotalRequests { get; set; }
        public int ApprovedRequests { get; set; }
        public int RejectedRequests { get; set; }
        public int PendingRequests { get; set; }
        public decimal TotalSpending { get; set; }
        public int PaidInvoices { get; set; }
        public int UnpaidInvoices { get; set; }

        // 📈 FILTER STATE
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        // 💎 BI DATA
        public List<ChartDataPoint> SpendingByCategory { get; set; } = new();
        public List<ChartDataPoint> MonthlySpendingTrend { get; set; } = new();
        public List<ChartDataPoint> RequestStatusDistribution { get; set; } = new();
        public List<ResourceRequest> RecentApprovals { get; set; } = new();
    }
}
