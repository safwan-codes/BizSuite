using System.Data;
using BizSuite.Models;

namespace BizSuite.Repositories
{
    public interface IProductRepository
    {
        IEnumerable<Product> GetAll(int companyId);
        Product? GetById(int productId, int companyId);
        void Insert(Product product);
        void Update(Product product);
        void Delete(int productId, int companyId);
        int UpsertCategory(int companyId, string categoryName);
    }
}
