using BizSuite.Models;

namespace BizSuite.Repositories
{
    public interface ICustomerRepository
    {
        IEnumerable<Customer> GetAll(int companyId);
        Customer? GetById(int customerId, int companyId);
        void Insert(Customer customer);
        void Update(Customer customer);
        void Delete(int customerId, int companyId);
    }
}
