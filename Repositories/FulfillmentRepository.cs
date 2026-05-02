using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using BizSuite.Models;
using BizSuite.Data;

namespace BizSuite.Repositories
{
    public class FulfillmentRepository : IFulfillmentRepository
    {
        private readonly DbHelper _db;

        public FulfillmentRepository(DbHelper db)
        {
            _db = db;
        }

        public int LogWebhookEvent(string provider, string eventType, string payload, int? salesOrderId)
        {
            string sql = @"
                INSERT INTO WebhookLogs (Provider, EventType, Payload, SalesOrderId, ReceivedAt, ProcessedStatus)
                VALUES (@Provider, @EventType, @Payload, @SalesOrderId, GETDATE(), 'Pending');
                SELECT CAST(SCOPE_IDENTITY() as int);
            ";
            
            var result = _db.ExecuteScalar(sql, 
                DbHelper.P("@Provider", provider),
                DbHelper.P("@EventType", eventType),
                DbHelper.P("@Payload", payload),
                DbHelper.P("@SalesOrderId", salesOrderId.HasValue ? (object)salesOrderId.Value : DBNull.Value)
            );
            
            return result != DBNull.Value ? Convert.ToInt32(result) : 0;
        }

        public bool UpdateWebhookStatus(int logId, string status)
        {
            string sql = "UPDATE WebhookLogs SET ProcessedStatus = @Status WHERE LogId = @LogId";
            return _db.ExecuteNonQuery(sql, 
                DbHelper.P("@Status", status), 
                DbHelper.P("@LogId", logId)
            ) > 0;
        }

        public bool UpdateOrderFulfillmentStatus(int orderId, string fulfillmentStatus, string trackingNumber = null, string carrier = null)
        {
            List<string> setClauses = new List<string> { "FulfillmentStatus = @FulfillmentStatus" };
            var paramList = new List<SqlParameter> {
                DbHelper.P("@FulfillmentStatus", fulfillmentStatus),
                DbHelper.P("@OrderId", orderId)
            };

            if (!string.IsNullOrEmpty(trackingNumber))
            {
                setClauses.Add("TrackingNumber = @TrackingNumber");
                paramList.Add(DbHelper.P("@TrackingNumber", trackingNumber));
            }
            if (!string.IsNullOrEmpty(carrier))
            {
                setClauses.Add("Carrier = @Carrier");
                paramList.Add(DbHelper.P("@Carrier", carrier));
            }

            if (fulfillmentStatus == "Shipped" || fulfillmentStatus == "Delivered")
            {
                setClauses.Add("ShippedDate = GETDATE()");
            }
            else if (fulfillmentStatus == "Pending" || fulfillmentStatus == "Processing" || fulfillmentStatus == "Packed")
            {
                setClauses.Add("ShippedDate = NULL");
            }

            string sql = $"UPDATE SalesOrders SET {string.Join(", ", setClauses)} WHERE SalesOrderId = @OrderId";
            
            return _db.ExecuteNonQuery(sql, paramList.ToArray()) > 0;
        }

        public IEnumerable<SalesOrderRow> GetPendingFulfillments()
        {
            string sql = @"
                SELECT 
                    SO.SalesOrderId as OrderId,
                    SO.OrderDate,
                    C.CustomerName as Customer,
                    C.Email,
                    C.Phone,
                    C.Address,
                    (SELECT ISNULL(SUM(Quantity), 1) FROM OrderItems WHERE SalesOrderId = SO.SalesOrderId) as Items,
                    (SELECT STRING_AGG(P.ProductName, ', ') FROM OrderItems OI JOIN Products P ON OI.ProductId = P.ProductId WHERE OI.SalesOrderId = SO.SalesOrderId) as ProductNames,
                    SO.TotalAmount as Total,
                    SO.Status,
                    SO.FulfillmentStatus,
                    SO.Carrier,
                    SO.TrackingNumber,
                    SO.ShippedDate
                FROM SalesOrders SO
                LEFT JOIN Customers C ON SO.CustomerId = C.CustomerId
                WHERE SO.FulfillmentStatus IN ('Pending', 'Processing', 'Packed')
                AND SO.IsDeleted = 0
                ORDER BY SO.OrderDate DESC
            ";
            
            var dt = _db.ExecuteQuery(sql);
            var list = new List<SalesOrderRow>();
            foreach (DataRow r in dt.Rows)
            {
                list.Add(MapToSalesOrderRow(r));
            }
            return list;
        }

        public SalesOrderRow GetOrderForFulfillment(int orderId)
        {
            string sql = @"
                SELECT 
                    SO.SalesOrderId as OrderId,
                    SO.OrderDate,
                    C.CustomerName as Customer,
                    C.Email,
                    C.Phone,
                    C.Address,
                    (SELECT ISNULL(SUM(Quantity), 1) FROM OrderItems WHERE SalesOrderId = SO.SalesOrderId) as Items,
                    (SELECT STRING_AGG(P.ProductName, ', ') FROM OrderItems OI JOIN Products P ON OI.ProductId = P.ProductId WHERE OI.SalesOrderId = SO.SalesOrderId) as ProductNames,
                    SO.TotalAmount as Total,
                    SO.Status,
                    SO.FulfillmentStatus,
                    SO.Carrier,
                    SO.TrackingNumber,
                    SO.ShippedDate
                FROM SalesOrders SO
                LEFT JOIN Customers C ON SO.CustomerId = C.CustomerId
                WHERE SO.SalesOrderId = @OrderId
                AND SO.IsDeleted = 0
            ";
            
            var dt = _db.ExecuteQuery(sql, DbHelper.P("@OrderId", orderId));
            if (dt.Rows.Count > 0)
            {
                return MapToSalesOrderRow(dt.Rows[0]);
            }
            return null;
        }

        private SalesOrderRow MapToSalesOrderRow(DataRow r)
        {
            return new SalesOrderRow
            {
                OrderId = Convert.ToInt32(r["OrderId"]),
                OrderDate = r["OrderDate"] != DBNull.Value ? Convert.ToDateTime(r["OrderDate"]).ToString("o") : "",
                Customer = r["Customer"] != DBNull.Value ? r["Customer"].ToString() : "Unknown",
                Email = r.Table.Columns.Contains("Email") && r["Email"] != DBNull.Value ? r["Email"].ToString() : "no-email@example.com",
                Phone = r.Table.Columns.Contains("Phone") && r["Phone"] != DBNull.Value ? r["Phone"].ToString() : "N/A",
                Address = r.Table.Columns.Contains("Address") && r["Address"] != DBNull.Value ? r["Address"].ToString() : "Unknown Address",
                Items = r["Items"] != DBNull.Value ? Convert.ToInt32(r["Items"]) : 0,
                ProductNames = r.Table.Columns.Contains("ProductNames") && r["ProductNames"] != DBNull.Value ? r["ProductNames"].ToString() : "",
                Total = r["Total"] != DBNull.Value ? Convert.ToDecimal(r["Total"]) : 0m,
                Status = r["Status"] != DBNull.Value ? r["Status"].ToString() : "Pending",
                FulfillmentStatus = r["FulfillmentStatus"] != DBNull.Value ? r["FulfillmentStatus"].ToString() : "Pending",
                Carrier = r["Carrier"] != DBNull.Value ? r["Carrier"].ToString() : "",
                TrackingNumber = r["TrackingNumber"] != DBNull.Value ? r["TrackingNumber"].ToString() : "",
                ShippedDate = r["ShippedDate"] != DBNull.Value ? Convert.ToDateTime(r["ShippedDate"]).ToString("o") : ""
            };
        }

        public void ProcessAutomatedFulfillments()
        {
            // Simulate logistics delay for demo purposes (Processing -> Shipped in hours, Shipped -> Delivered in a day)
            string sqlProcessToShipped = @"
                UPDATE SalesOrders 
                SET FulfillmentStatus = 'Shipped', ShippedDate = GETDATE(), Carrier = 'AutoDemoCarrier', TrackingNumber = 'TRK' + CAST(SalesOrderId as varchar)
                WHERE FulfillmentStatus = 'Processing' AND DATEDIFF(hour, OrderDate, GETDATE()) >= 2 AND IsDeleted = 0";
                
            string sqlShippedToDelivered = @"
                UPDATE SalesOrders 
                SET FulfillmentStatus = 'Delivered'
                WHERE FulfillmentStatus = 'Shipped' AND DATEDIFF(hour, ShippedDate, GETDATE()) >= 24 AND IsDeleted = 0";

            _db.ExecuteNonQuery(sqlProcessToShipped);
            _db.ExecuteNonQuery(sqlShippedToDelivered);
        }

        public OrderDetailViewModel GetOrderDetailsForFulfillment(int orderId)
        {
            var header = GetOrderForFulfillment(orderId);
            if (header == null) return null;

            var vm = new OrderDetailViewModel { Header = header };
            
            string itemsSql = @"
                SELECT 
                    P.ProductName,
                    OI.Quantity,
                    OI.UnitPrice,
                    OI.LineTotal
                FROM OrderItems OI
                JOIN Products P ON OI.ProductId = P.ProductId
                WHERE OI.SalesOrderId = @OrderId
            ";
            
            var dtItems = _db.ExecuteQuery(itemsSql, DbHelper.P("@OrderId", orderId));
            foreach (DataRow r in dtItems.Rows)
            {
                vm.Items.Add(new OrderItemDetail
                {
                    ProductName = r["ProductName"].ToString() ?? "Unknown",
                    Quantity = r["Quantity"] != DBNull.Value ? Convert.ToInt32(r["Quantity"]) : 0,
                    UnitPrice = r["UnitPrice"] != DBNull.Value ? Convert.ToDecimal(r["UnitPrice"]) : 0m,
                    LineTotal = r["LineTotal"] != DBNull.Value ? Convert.ToDecimal(r["LineTotal"]) : 0m
                });
            }

            return vm;
        }
    }
}
