namespace Latog_Final_project.Models
{
    public class DashboardViewModel
    {
        public int TotalUsers { get; set; }
        public int TotalTransactions { get; set; }
        public int TotalBranches { get; set; }
        public List<AuditLog> RecentLogs { get; set; } = new();

        // 💎 GLOBAL BI DATA
        public List<ChartDataPoint> BranchActivity { get; set; } = new();
        public List<ChartDataPoint> GlobalSystemActivity { get; set; } = new();
        public List<ChartDataPoint> UserRoleDistribution { get; set; } = new();

        // 📅 FILTER STATE
        public int? FilterYear { get; set; }
        public int? FilterMonth { get; set; }
    }
}
