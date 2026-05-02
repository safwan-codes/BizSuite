using Microsoft.AspNetCore.Http; 

namespace BizSuite.Models
{
    public class Product
    {
        
        public int ProductId { get; set; }
        public int CompanyId { get; set; }
        public int? CategoryId { get; set; }
        public int? SupplierId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public int LowStockThreshold { get; set; }
        public bool IsDeleted { get; set; }

        public int? CreatedBy { get; set; }

        public string? SKU { get; set; }
        public string? HSNCode { get; set; }
        public decimal TaxRate { get; set; }
        public string? ImageUrl { get; set; }

        public byte[]? ImageData { get; set; }
        public string? ImageMimeType { get; set; }
        public IFormFile? ImageFile { get; set; }

        public bool IsPublished { get; set; } = true;

        public string? CategoryName { get; set; }
        public string? SupplierName { get; set; }
        public string StockStatus { get; set; } = "Unknown";
    }
}