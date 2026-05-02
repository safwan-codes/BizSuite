namespace BizSuite.Models.Staff
{
    public class StaffMember
    {
        public int    StaffId    { get; set; }
        public int    CompanyId  { get; set; }
        public string FullName   { get; set; } = string.Empty;
        public string Email      { get; set; } = string.Empty;
        public string Phone      { get; set; } = string.Empty;
        public string Role       { get; set; } = "Staff";
        public string Department { get; set; } = string.Empty;
        public string JoinDate   { get; set; } = string.Empty;
        public bool   IsActive   { get; set; } = true;
    }
}
