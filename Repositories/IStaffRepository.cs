using BizSuite.Models.Staff;

namespace BizSuite.Repositories
{
    public interface IStaffRepository
    {
        IEnumerable<StaffMember> GetAll(int companyId);
        StaffMember? GetById(int staffId, int companyId);
        void Insert(StaffMember staff);
        void Update(StaffMember staff);
        void Delete(int staffId, int companyId);
    }
}
