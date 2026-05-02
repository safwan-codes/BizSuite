namespace BizSuite.Models.Dashboard
{
    public class AdminDashboardViewModel
    {
        public string UserFullName { get; set; } = "Admin";
        public string CompanyName { get; set; } = "Your Company";

        public decimal TotalSales { get; set; }
        public decimal InvoicedAmount { get; set; }
        public int LowStockCount { get; set; }
        public int TotalCustomers { get; set; }
        public int TotalProducts { get; set; }
        public decimal TotalOrders { get; set; }
        public decimal PendingPayments { get; set; }

        public decimal SalesThisMonth { get; set; }
        public decimal SalesLastMonth { get; set; }
        public double SalesGrowth => SalesLastMonth == 0 ? 0 : (double)((SalesThisMonth - SalesLastMonth) / SalesLastMonth * 100);

        public List<string> MonthLabels { get; set; } = new();
        public List<decimal> MonthlyRevenue { get; set; } = new();
        public List<CategorySalesRow> CategorySales { get; set; } = new();

        public List<RecentOrderRow> RecentOrders { get; set; } = new();

        public List<TopCustomerRow> TopCustomers { get; set; } = new();

        public List<TopProductRow> TopProducts { get; set; } = new();
        public string ReportPeriod { get; set; } = "Current Status";
        public string ReportType { get; set; } = "Executive Dashboard";
    }

    public class TopProductRow
    {
        public string ProductName { get; set; } = string.Empty;
        public int UnitsSold { get; set; }
        public decimal Revenue { get; set; }
    }

    public class CategorySalesRow
    {
        public string CategoryName { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
    }

    public class RecentOrderRow
    {
        public int OrderId { get; set; }
        public string Customer { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class TopCustomerRow
    {
        public string CustomerName { get; set; } = string.Empty;
        public int Orders { get; set; }
        public decimal TotalSpend { get; set; }
    }
}