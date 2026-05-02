namespace BizSuite.Models
{
    public class Customer
    {
        public int CustomerId { get; set; }
        public int CompanyId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public int Orders { get; set; }
        public decimal Spend { get; set; }
        public bool IsDeleted { get; set; }
        public int CreatedBy { get; set; }

        public byte[]? ImageData { get; set; }
        public string? ImageMimeType { get; set; }
        public Microsoft.AspNetCore.Http.IFormFile? ImageFile { get; set; }
    }
}
