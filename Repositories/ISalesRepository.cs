using System.Collections.Generic;
using BizSuite.Models;

namespace BizSuite.Repositories
{
    public interface ISalesRepository
    {
        IEnumerable<SalesOrderRow> GetSalesOrders(int companyId);
        PagedResult<SalesOrderRow> GetSalesOrdersPaged(int companyId, SalesOrderQueryParameters queryParams);
        IEnumerable<InvoiceRow> GetInvoices(int companyId);
        (int orderId, int invoiceId) CreateSalesOrder(int companyId, int customerId, int userId, List<OrderItemDto> items);
        IEnumerable<SalesOrderRow> GetPendingOrders(int companyId);
        void UpdateFulfillmentStatus(int companyId, int orderId, string status, string carrier = null, string trackingNumber = null);
        OrderDetailViewModel GetOrderDetail(int companyId, int orderId);
    }

    public class OrderItemDto
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}