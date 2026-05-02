namespace BizSuite.Models
{
    public class SalesOrderRow
    {
        public int OrderId { get; set; }
        public string OrderDate { get; set; } = string.Empty;
        public string Customer { get; set; } = string.Empty;
        public int Items { get; set; } = 1;
        public string ProductNames { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = "Cash on delivery (COD)";
        public decimal Total { get; set; }
        public string Status { get; set; } = string.Empty;
        public string FulfillmentStatus { get; set; } = "Pending";
        public string Carrier { get; set; } = string.Empty;
        public string TrackingNumber { get; set; } = string.Empty;
        public string ShippedDate { get; set; } = string.Empty;
    }

    public class InvoiceRow
    {
        public int InvoiceId { get; set; }
        public string InvoiceDate { get; set; } = string.Empty;
        public string Customer { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
    }

    public class OrderDetailViewModel
    {
        public SalesOrderRow Header { get; set; } = new();
        public List<OrderItemDetail> Items { get; set; } = new();
        public InvoiceRow Invoice { get; set; }
    }

    public class OrderItemDetail
    {
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }
}
