using System;
using System.Collections.Generic;

namespace Latog_Final_project.Models
{
    public class TreasurerDashboardViewModel
    {
        // TOP Section
        public decimal TotalSpending { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal UnpaidAmount { get; set; }
        public int TotalInvoices { get; set; }
        public int PendingRequests { get; set; }
        public int OverdueInvoices { get; set; }

        // MIDDLE Section
        public List<Invoice> PendingPayments { get; set; } = new();

        // CHARTS Section
        public string FilterPeriod { get; set; } = "Monthly";
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public List<ChartDataPoint> SpendingChartData { get; set; } = new();
        public List<ChartDataPoint> InvoiceStatusData { get; set; } = new();
        
        // 💎 NEW PREMIUM DATA
        public List<ChartDataPoint> CategoryDistribution { get; set; } = new();
        public List<ChartDataPoint> ItemVolumeData { get; set; } = new();
        public List<ChartDataPoint> PeakHoursData { get; set; } = new();
        public List<TopPerformingProduct> TopProducts { get; set; } = new();

        // Helper for AJAX
        public List<string> ChartLabels => SpendingChartData.Select(d => d.Label).ToList();
        public List<decimal> ChartValues => SpendingChartData.Select(d => d.Value).ToList();
    }
}
