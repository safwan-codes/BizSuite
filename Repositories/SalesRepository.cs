using System;
using System.Collections.Generic;
using System.Data;
using BizSuite.Models;
using BizSuite.Data;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;

namespace BizSuite.Repositories
{
    public class SalesRepository : ISalesRepository
    {
        private readonly DbHelper _db;

        public SalesRepository(DbHelper db)
        {
            _db = db;
        }

        public IEnumerable<SalesOrderRow> GetSalesOrders(int companyId)
        {
            var dt = _db.ExecuteQuery("SELECT * FROM vw_SalesSummary WHERE CompanyId = @CompanyId ORDER BY OrderDate DESC", DbHelper.P("@CompanyId", companyId));
            var list = new List<SalesOrderRow>();
            foreach (DataRow r in dt.Rows)
            {
                list.Add(new SalesOrderRow
                {
                    OrderId = Convert.ToInt32(r["SalesOrderId"]),
                    OrderDate = r["OrderDate"] != DBNull.Value ? Convert.ToDateTime(r["OrderDate"]).ToString("dd MMM yyyy") : "",
                    Customer = r["CustomerName"].ToString() ?? "",
                    Items = r.Table.Columns.Contains("TotalItems") && r["TotalItems"] != DBNull.Value ? Convert.ToInt32(r["TotalItems"]) : 0,
                    Total = r["TotalAmount"] != DBNull.Value ? Convert.ToDecimal(r["TotalAmount"]) : 0m,
                    Status = r["InvoiceStatus"].ToString() ?? "Pending",
                    FulfillmentStatus = r.Table.Columns.Contains("FulfillmentStatus") ? r["FulfillmentStatus"].ToString() : "Delivered",
                    Carrier = r.Table.Columns.Contains("Carrier") && r["Carrier"] != DBNull.Value ? r["Carrier"].ToString() : "",
                    TrackingNumber = r.Table.Columns.Contains("TrackingNumber") && r["TrackingNumber"] != DBNull.Value ? r["TrackingNumber"].ToString() : "",
                    ShippedDate = r.Table.Columns.Contains("ShippedDate") && r["ShippedDate"] != DBNull.Value ? Convert.ToDateTime(r["ShippedDate"]).ToString("dd MMM yyyy HH:mm") : ""
                });
            }
            return list;
        }

        public PagedResult<SalesOrderRow> GetSalesOrdersPaged(int companyId, SalesOrderQueryParameters queryParams)
        {
            var pList = new List<SqlParameter> { DbHelper.P("@CompanyId", companyId) };
            string whereClause = "WHERE CompanyId = @CompanyId";

            if (!string.IsNullOrEmpty(queryParams.SearchTerm))
            {
                whereClause += " AND (CustomerName LIKE @Search OR CAST(SalesOrderId AS VARCHAR) LIKE @Search)";
                pList.Add(DbHelper.P("@Search", $"%{queryParams.SearchTerm}%"));
            }

            if (!string.IsNullOrEmpty(queryParams.FulfillmentStatus))
            {
                whereClause += " AND FulfillmentStatus = @Status";
                pList.Add(DbHelper.P("@Status", queryParams.FulfillmentStatus));
            }

            string sortCol = queryParams.SortBy == "CustomerName" ? "CustomerName" : "OrderDate";
            string sortDir = queryParams.SortDirection == "ASC" ? "ASC" : "DESC";

            string countSql = $"SELECT COUNT(*) FROM vw_SalesSummary {whereClause}";
            int totalCount = Convert.ToInt32(_db.ExecuteQuery(countSql, pList.ToArray()).Rows[0][0]);

            // Clone parameters for main query (Parameters cannot be reused across commands without clearing, but ExecuteQuery internally doesn't clear them, it passes them to command. We should recreate them)
            var pListQuery = new List<SqlParameter> { DbHelper.P("@CompanyId", companyId) };
            if (!string.IsNullOrEmpty(queryParams.SearchTerm))
                pListQuery.Add(DbHelper.P("@Search", $"%{queryParams.SearchTerm}%"));
            if (!string.IsNullOrEmpty(queryParams.FulfillmentStatus))
                pListQuery.Add(DbHelper.P("@Status", queryParams.FulfillmentStatus));

            string dataSql = $@"
                SELECT * FROM vw_SalesSummary
                {whereClause}
                ORDER BY {sortCol} {sortDir}
                OFFSET @Offset ROWS
                FETCH NEXT @PageSize ROWS ONLY";

            pListQuery.Add(DbHelper.P("@Offset", (queryParams.PageNumber - 1) * queryParams.PageSize));
            pListQuery.Add(DbHelper.P("@PageSize", queryParams.PageSize));

            var dt = _db.ExecuteQuery(dataSql, pListQuery.ToArray());

            var list = new List<SalesOrderRow>();
            foreach (DataRow r in dt.Rows)
            {
                list.Add(new SalesOrderRow
                {
                    OrderId = Convert.ToInt32(r["SalesOrderId"]),
                    OrderDate = r["OrderDate"] != DBNull.Value ? Convert.ToDateTime(r["OrderDate"]).ToString("dd MMM yyyy") : "",
                    Customer = r["CustomerName"].ToString() ?? "",
                    Items = r.Table.Columns.Contains("TotalItems") && r["TotalItems"] != DBNull.Value ? Convert.ToInt32(r["TotalItems"]) : 0,
                    Total = r["TotalAmount"] != DBNull.Value ? Convert.ToDecimal(r["TotalAmount"]) : 0m,
                    Status = r["InvoiceStatus"].ToString() ?? "Pending",
                    FulfillmentStatus = r.Table.Columns.Contains("FulfillmentStatus") ? r["FulfillmentStatus"].ToString() : "Delivered",
                    Carrier = r.Table.Columns.Contains("Carrier") && r["Carrier"] != DBNull.Value ? r["Carrier"].ToString() : "",
                    TrackingNumber = r.Table.Columns.Contains("TrackingNumber") && r["TrackingNumber"] != DBNull.Value ? r["TrackingNumber"].ToString() : "",
                    ShippedDate = r.Table.Columns.Contains("ShippedDate") && r["ShippedDate"] != DBNull.Value ? Convert.ToDateTime(r["ShippedDate"]).ToString("dd MMM yyyy HH:mm") : ""
                });
            }

            return new PagedResult<SalesOrderRow>
            {
                Items = list,
                TotalCount = totalCount,
                PageNumber = queryParams.PageNumber,
                PageSize = queryParams.PageSize
            };
        }

        public IEnumerable<SalesOrderRow> GetPendingOrders(int companyId)
        {
            var dt = _db.ExecuteQuery("SELECT * FROM vw_SalesSummary WHERE CompanyId = @CompanyId AND FulfillmentStatus <> 'Delivered' ORDER BY OrderDate ASC", DbHelper.P("@CompanyId", companyId));
            var list = new List<SalesOrderRow>();
            foreach (DataRow r in dt.Rows)
            {
                list.Add(new SalesOrderRow
                {
                    OrderId = Convert.ToInt32(r["SalesOrderId"]),
                    OrderDate = r["OrderDate"] != DBNull.Value ? Convert.ToDateTime(r["OrderDate"]).ToString("dd MMM yyyy") : "",
                    Customer = r["CustomerName"].ToString() ?? "",
                    Items = r.Table.Columns.Contains("TotalItems") && r["TotalItems"] != DBNull.Value ? Convert.ToInt32(r["TotalItems"]) : 0,
                    Total = r["TotalAmount"] != DBNull.Value ? Convert.ToDecimal(r["TotalAmount"]) : 0m,
                    Status = r["InvoiceStatus"].ToString() ?? "Pending",
                    FulfillmentStatus = r["FulfillmentStatus"].ToString() ?? "Pending",
                    Carrier = r.Table.Columns.Contains("Carrier") && r["Carrier"] != DBNull.Value ? r["Carrier"].ToString() : "",
                    TrackingNumber = r.Table.Columns.Contains("TrackingNumber") && r["TrackingNumber"] != DBNull.Value ? r["TrackingNumber"].ToString() : "",
                    ShippedDate = r.Table.Columns.Contains("ShippedDate") && r["ShippedDate"] != DBNull.Value ? Convert.ToDateTime(r["ShippedDate"]).ToString("dd MMM yyyy HH:mm") : ""
                });
            }
            return list;
        }

        public void UpdateFulfillmentStatus(int companyId, int orderId, string status, string carrier = null, string trackingNumber = null)
        {
            string sql = "UPDATE SalesOrders SET FulfillmentStatus = @S";
            
            if (status == "Shipped")
            {
                sql += ", Carrier = @Carrier, TrackingNumber = @Tracking, ShippedDate = GETDATE()";
            }
            else if (status == "Delivered")
            {
                // Optionally log delivery date? For now, we only log ShippedDate
            }

            sql += " WHERE SalesOrderId = @O AND CompanyId = @C";

            _db.ExecuteNonQuery(sql,
                DbHelper.P("@S", status),
                DbHelper.P("@O", orderId),
                DbHelper.P("@C", companyId),
                DbHelper.P("@Carrier", carrier ?? (object)DBNull.Value),
                DbHelper.P("@Tracking", trackingNumber ?? (object)DBNull.Value));

            _db.LogAudit(companyId, "SalesOrders", "UPDATE", $"Order #{orderId} status changed to {status}. Tracking: {trackingNumber}");
            
            if (status == "Shipped")
            {
                _db.AddNotification(companyId, null, "Order Shipped", $"Order #{orderId} has been shipped via {(string.IsNullOrEmpty(carrier) ? "Standard" : carrier)}.", "info");
            }
            if (status == "Delivered")
            {
                _db.AddNotification(companyId, null, "Order Delivered", $"Order #{orderId} has arrived at its destination.", "success");
            }
        }

        public OrderDetailViewModel GetOrderDetail(int companyId, int orderId)
        {
            var vm = new OrderDetailViewModel();
            
            var headerDt = _db.ExecuteQuery("SELECT * FROM vw_SalesSummary WHERE CompanyId = @C AND SalesOrderId = @O",
                DbHelper.P("@C", companyId), DbHelper.P("@O", orderId));
            
            if (headerDt.Rows.Count > 0)
            {
                var r = headerDt.Rows[0];
                vm.Header = new SalesOrderRow
                {
                    OrderId = Convert.ToInt32(r["SalesOrderId"]),
                    OrderDate = Convert.ToDateTime(r["OrderDate"]).ToString("dd MMM yyyy"),
                    Customer = r["CustomerName"].ToString(),
                    Total = Convert.ToDecimal(r["TotalAmount"]),
                    FulfillmentStatus = r["FulfillmentStatus"].ToString()
                };
            }

            var itemsDt = _db.ExecuteQuery(@"
                SELECT P.ProductName, OI.Quantity, OI.UnitPrice, OI.LineTotal 
                FROM OrderItems OI 
                JOIN Products P ON OI.ProductId = P.ProductId 
                WHERE OI.CompanyId = @C AND OI.SalesOrderId = @O",
                DbHelper.P("@C", companyId), DbHelper.P("@O", orderId));

            foreach (DataRow r in itemsDt.Rows)
            {
                vm.Items.Add(new OrderItemDetail
                {
                    ProductName = r["ProductName"].ToString(),
                    Quantity = Convert.ToInt32(r["Quantity"]),
                    UnitPrice = Convert.ToDecimal(r["UnitPrice"]),
                    LineTotal = Convert.ToDecimal(r["LineTotal"])
                });
            }

            return vm;
        }

        public IEnumerable<InvoiceRow> GetInvoices(int companyId)
        {
            var dt = _db.ExecuteQuery("sp_GetInvoices", DbHelper.P("@CompanyId", companyId));
            var list = new List<InvoiceRow>();
            foreach (DataRow r in dt.Rows)
            {
                list.Add(new InvoiceRow
                {
                    InvoiceId = Convert.ToInt32(r["InvoiceId"]),
                    InvoiceDate = r["InvoiceDate"] != DBNull.Value ? Convert.ToDateTime(r["InvoiceDate"]).ToString("dd MMM yyyy") : "",
                    Customer = r["CustomerName"].ToString() ?? "",
                    Amount = r["TotalAmount"] != DBNull.Value ? Convert.ToDecimal(r["TotalAmount"]) : 0m,
                    Status = r["Status"].ToString() ?? "Pending",
                    Method = r["Method"] != DBNull.Value ? r["Method"].ToString() : "—"
                });
            }
            return list;
        }

        public (int orderId, int invoiceId) CreateSalesOrder(int companyId, int customerId, int userId, List<OrderItemDto> items)
        {
            if (items == null || items.Count == 0)
                throw new ArgumentException("An order must contain at least one item.");

            string itemsJson = JsonConvert.SerializeObject(items);
            var pOrderId = new SqlParameter("@NewOrderId", SqlDbType.Int) { Direction = ParameterDirection.Output };
            var pInvoiceId = new SqlParameter("@NewInvoiceId", SqlDbType.Int) { Direction = ParameterDirection.Output };

            _db.ExecuteNonQuery("sp_CreateSalesOrder",
                DbHelper.P("@CompanyId", companyId),
                DbHelper.P("@CustomerId", customerId),
                DbHelper.P("@OrderDate", DateTime.Now),
                DbHelper.P("@OrderItemsJson", itemsJson),
                DbHelper.P("@UserId", userId),
                pOrderId,
                pInvoiceId
            );

            int newOrderId = (int)pOrderId.Value;
            int newInvoiceId = (int)pInvoiceId.Value;

            _db.AddNotification(companyId, null, "New Sales Order", $"Order #{newOrderId} has been placed containing {items.Count} product lines.", "info");

            return (newOrderId, newInvoiceId);
        }
    }
}