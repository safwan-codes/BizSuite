using BizSuite.Models;
using System.Collections.Generic;

namespace BizSuite.Repositories
{
    public interface IFulfillmentRepository
    {
        int LogWebhookEvent(string provider, string eventType, string payload, int? salesOrderId);
        bool UpdateWebhookStatus(int logId, string status);
        bool UpdateOrderFulfillmentStatus(int orderId, string fulfillmentStatus, string trackingNumber = null, string carrier = null);
        IEnumerable<SalesOrderRow> GetPendingFulfillments();
        SalesOrderRow GetOrderForFulfillment(int orderId);
        OrderDetailViewModel GetOrderDetailsForFulfillment(int orderId);
        void ProcessAutomatedFulfillments();
    }
}
