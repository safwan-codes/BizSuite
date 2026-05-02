using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using BizSuite.Models.Dashboard;
using BizSuite.Data;
using System.Linq;

namespace BizSuite.Repositories
{
    public class DashboardRepository : IDashboardRepository
    {
        private readonly DbHelper _db;

        public DashboardRepository(DbHelper db)
        {
            _db = db;
        }

        public AdminDashboardViewModel GetAdminStats(int companyId, string fullName, string company, DateTime? startDate = null, DateTime? endDate = null)
        {
            var ds = _db.ExecuteDataSet("sp_GetDashboardStats",
                DbHelper.P("@CompanyId", companyId));

            var vm = new AdminDashboardViewModel
            {
                UserFullName = fullName ?? "Admin",
                CompanyName = company ?? "Company",
                TopCustomers = new List<TopCustomerRow>(),
                RecentOrders = new List<RecentOrderRow>()
            };

            if (ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
            {
                var row = ds.Tables[0].Rows[0];
                vm.TotalCustomers = row["TotalCustomers"] != DBNull.Value ? Convert.ToInt32(row["TotalCustomers"]) : 0;
                vm.TotalProducts = row["TotalProducts"] != DBNull.Value ? Convert.ToInt32(row["TotalProducts"]) : 0;
                vm.TotalSales = row["TotalSales"] != DBNull.Value ? Convert.ToDecimal(row["TotalSales"]) : 0m;
                vm.SalesThisMonth = row["SalesThisMonth"] != DBNull.Value ? Convert.ToDecimal(row["SalesThisMonth"]) : 0m;
                vm.SalesLastMonth = row["SalesLastMonth"] != DBNull.Value ? Convert.ToDecimal(row["SalesLastMonth"]) : 0m;
            }

            if (ds.Tables.Count > 1) vm.LowStockCount = ds.Tables[1].Rows.Count;

            if (ds.Tables.Count > 2)
            {
                foreach (DataRow r in ds.Tables[2].Rows)
                {
                    vm.TopCustomers.Add(new TopCustomerRow
                    {
                        CustomerName = r["CustomerName"].ToString() ?? "Unknown",
                        TotalSpend = r["TotalPurchase"] != DBNull.Value ? Convert.ToDecimal(r["TotalPurchase"]) : 0m,
                        Orders = r.Table.Columns.Contains("OrderCount") && r["OrderCount"] != DBNull.Value ? Convert.ToInt32(r["OrderCount"]) : 0
                    });
                }
            }

            if (ds.Tables.Count > 3)
            {
                foreach (DataRow r in ds.Tables[3].Rows)
                {
                    vm.CategorySales.Add(new CategorySalesRow
                    {
                        CategoryName = r["CategoryName"].ToString() ?? "Uncategorized",
                        Revenue = r["Revenue"] != DBNull.Value ? Convert.ToDecimal(r["Revenue"]) : 0m
                    });
                }
            }

            vm.MonthLabels = new List<string>();
            vm.MonthlyRevenue = new List<decimal>();

            var last12Months = new Dictionary<string, decimal>();
            for (int i = 11; i >= 0; i--)
            {
                var d = DateTime.Now.AddMonths(-i);
                last12Months[d.ToString("MMM")] = 0m;
            }

            if (ds.Tables.Count > 4)
            {
                foreach (DataRow r in ds.Tables[4].Rows)
                {
                    int year = Convert.ToInt32(r["Year"]);
                    int month = Convert.ToInt32(r["Month"]);
                    decimal total = r["TotalSales"] != DBNull.Value ? Convert.ToDecimal(r["TotalSales"]) : 0m;

                    var mLabel = new DateTime(year, month, 1).ToString("MMM");
                    if (last12Months.ContainsKey(mLabel))
                    {
                        last12Months[mLabel] = total;
                    }
                }
            }

            foreach (var kvp in last12Months)
            {
                vm.MonthLabels.Add(kvp.Key);
                vm.MonthlyRevenue.Add(kvp.Value);
            }

            if (ds.Tables.Count > 5)
            {
                foreach (DataRow r in ds.Tables[5].Rows)
                {
                    vm.TopProducts.Add(new TopProductRow
                    {
                        ProductName = r["ProductName"].ToString() ?? "Unknown",
                        UnitsSold = r["UnitsSold"] != DBNull.Value ? Convert.ToInt32(r["UnitsSold"]) : 0,
                        Revenue = r["Revenue"] != DBNull.Value ? Convert.ToDecimal(r["Revenue"]) : 0m
                    });
                }
            }

            if (!vm.TopProducts.Any())
            {
                vm.TopProducts.Add(new TopProductRow { ProductName = "No sales yet", UnitsSold = 0, Revenue = 0m });
            }

            if (!vm.CategorySales.Any())
            {
                vm.CategorySales.Add(new CategorySalesRow { CategoryName = "No data", Revenue = 0m });
            }

            string aggregateSql = @"
                SELECT 
                    COUNT(SalesOrderId) AS OrderCount, 
                    ISNULL(SUM(TotalAmount), 0) AS TotalInvoiced, 
                    ISNULL(SUM(CASE WHEN Status = 'Pending' THEN TotalAmount ELSE 0 END), 0) AS PendingPayments
                FROM SalesOrders 
                WHERE CompanyId = @CompanyId AND IsDeleted = 0";

            var aggDt = _db.ExecuteQuery(aggregateSql, DbHelper.P("@CompanyId", companyId));
            if (aggDt.Rows.Count > 0)
            {
                vm.TotalOrders = Convert.ToInt32(aggDt.Rows[0]["OrderCount"]);
                vm.InvoicedAmount = Convert.ToDecimal(aggDt.Rows[0]["TotalInvoiced"]);
                vm.PendingPayments = Convert.ToDecimal(aggDt.Rows[0]["PendingPayments"]);
            }

            var recentSalesDt = _db.ExecuteQuery("SELECT TOP 5 * FROM vw_SalesSummary WHERE CompanyId = @CompanyId ORDER BY OrderDate DESC", DbHelper.P("@CompanyId", companyId));

            foreach (DataRow r in recentSalesDt.Rows)
            {
                vm.RecentOrders.Add(new RecentOrderRow
                {
                    OrderId = Convert.ToInt32(r["SalesOrderId"]),
                    Customer = r["CustomerName"].ToString() ?? "Unknown",
                    Date = r["OrderDate"] != DBNull.Value ? Convert.ToDateTime(r["OrderDate"]).ToString("dd MMM yyyy") : "",
                    Amount = r["TotalAmount"] != DBNull.Value ? Convert.ToDecimal(r["TotalAmount"]) : 0m,
                    Status = r["InvoiceStatus"].ToString() ?? "Pending"
                });
            }

            return vm;
        }

        public StaffDashboardViewModel GetStaffStats(int companyId, string fullName)
        {
            var vm = new StaffDashboardViewModel
            {
                UserFullName = fullName,
                ShiftStart = "9:00 AM",
                ActionItems = new(),
                LowStockAlerts = new()
            };

            var ordersDt = _db.ExecuteQuery("SELECT COUNT(SalesOrderId) FROM SalesOrders WHERE CompanyId = @C AND CAST(OrderDate AS DATE) = CAST(GETDATE() AS DATE)", DbHelper.P("@C", companyId));
            vm.OrdersToday = ordersDt.Rows.Count > 0 && ordersDt.Rows[0][0] != DBNull.Value ? Convert.ToInt32(ordersDt.Rows[0][0]) : 0;

            var ticketsDt = _db.ExecuteQuery("SELECT COUNT(NotificationId) FROM Notifications WHERE CompanyId = @C AND IsRead = 0", DbHelper.P("@C", companyId));
            vm.PendingTickets = ticketsDt.Rows.Count > 0 && ticketsDt.Rows[0][0] != DBNull.Value ? Convert.ToInt32(ticketsDt.Rows[0][0]) : 0;

            var stockDt = _db.ExecuteQuery("SELECT ProductName, ISNULL(StockQuantity, 0) as StockQuantity, LowStockThreshold FROM Products WHERE CompanyId = @C AND StockQuantity <= LowStockThreshold AND IsDeleted = 0", DbHelper.P("@C", companyId));
            foreach (DataRow r in stockDt.Rows)
            {
                vm.LowStockAlerts.Add(new LowStockItem
                {
                    ProductName = r["ProductName"].ToString() ?? "Unknown",
                    Remaining = Convert.ToInt32(r["StockQuantity"]),
                    Threshold = r["LowStockThreshold"] != DBNull.Value ? Convert.ToInt32(r["LowStockThreshold"]) : 10
                });
            }

            var actionDt = _db.ExecuteQuery("SELECT TOP 5 SalesOrderId, CustomerName, OrderDate FROM vw_SalesSummary WHERE CompanyId = @C AND InvoiceStatus = 'Pending' ORDER BY OrderDate DESC", DbHelper.P("@C", companyId));
            foreach (DataRow r in actionDt.Rows)
            {
                vm.ActionItems.Add(new ActionDeskItem
                {
                    OrderId = "#" + r["SalesOrderId"].ToString(),
                    Customer = r["CustomerName"].ToString() ?? "Unknown",
                    Priority = "High",
                    Time = r["OrderDate"] != DBNull.Value ? Convert.ToDateTime(r["OrderDate"]).ToString("dd MMM") : "Today"
                });
            }

            return vm;
        }
    }
}