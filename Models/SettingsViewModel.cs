using System.Collections.Generic;
using BizSuite.Models.Staff;

namespace BizSuite.Models
{
    public class SettingsViewModel
    {
        public int CompanyId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string Industry { get; set; } = string.Empty;
        public string AdminName { get; set; } = string.Empty;
        public string AdminEmail { get; set; } = string.Empty;
        public string BusinessAddress { get; set; } = string.Empty;
        public string GstNumber { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        
        public string SubscriptionPlan { get; set; } = "Free";

        public List<StaffMember> TeamMembers { get; set; } = new List<StaffMember>();
    }
}
