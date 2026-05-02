namespace BizSuite.Models.Dashboard
{
    public class StaffDashboardViewModel
    {
        public string UserFullName  { get; set; } = "Staff";
        public string ShiftStart    { get; set; } = "9:00 AM";
        public int    OrdersToday   { get; set; }
        public int    PendingTickets { get; set; }

        public bool      IsClockedIn   { get; set; } = false;
        public bool      IsClockedOut  { get; set; } = false;
        public DateTime? ClockInTime   { get; set; }
        public DateTime? ClockOutTime  { get; set; }
        public int       AttendanceId  { get; set; }

        public List<ActionDeskItem> ActionItems      { get; set; } = new();
        public List<LowStockItem>   LowStockAlerts   { get; set; } = new();
    }

    public class ActionDeskItem
    {
        public string OrderId    { get; set; } = string.Empty;
        public string Customer   { get; set; } = string.Empty;
        public string Priority   { get; set; } = "Medium";   
        public string Time       { get; set; } = string.Empty;
    }

    public class LowStockItem
    {
        public string ProductName { get; set; } = string.Empty;
        public string SKU         { get; set; } = string.Empty;
        public int    Remaining   { get; set; }
        public int    Threshold   { get; set; }
        public bool   IsCritical  => Remaining <= Threshold / 2;
    }
}
