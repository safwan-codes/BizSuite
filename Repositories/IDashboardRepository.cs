using BizSuite.Models.Dashboard;

namespace BizSuite.Repositories
{
    public interface IDashboardRepository
    {
        AdminDashboardViewModel GetAdminStats(int companyId, string fullName, string company, DateTime? startDate = null, DateTime? endDate = null);
        StaffDashboardViewModel GetStaffStats(int companyId, string fullName);
    }
}