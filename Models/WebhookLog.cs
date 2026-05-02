using System;

namespace BizSuite.Models
{
    public class WebhookLog
    {
        public int LogId { get; set; }
        public string Provider { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
        public DateTime ReceivedAt { get; set; }
        public string ProcessedStatus { get; set; } = "Pending";
        public int? SalesOrderId { get; set; }
    }
}
