using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using BizSuite.Data;
using BizSuite.Models;
using Microsoft.Data.SqlClient;

namespace BizSuite.Repositories
{
    public class ProductRepository : IProductRepository
    {
        private readonly DbHelper _db;

        public ProductRepository(DbHelper db)
        {
            _db = db;
        }

        public IEnumerable<Product> GetAll(int companyId)
        {
            var dt = _db.ExecuteQuery("sp_GetProducts", DbHelper.P("@CompanyId", companyId));
            var list = new List<Product>();
            foreach (DataRow r in dt.Rows)
            {
                list.Add(new Product
                {
                    ProductId = r.Table.Columns.Contains("ProductId") && r["ProductId"] != DBNull.Value ? Convert.ToInt32(r["ProductId"]) : 0,
                    CompanyId = r.Table.Columns.Contains("CompanyId") && r["CompanyId"] != DBNull.Value ? Convert.ToInt32(r["CompanyId"]) : 0,
                    CategoryId = r.Table.Columns.Contains("CategoryId") && r["CategoryId"] != DBNull.Value ? Convert.ToInt32(r["CategoryId"]) : null,
                    SupplierId = r.Table.Columns.Contains("SupplierId") && r["SupplierId"] != DBNull.Value ? Convert.ToInt32(r["SupplierId"]) : null,
                    ProductName = r.Table.Columns.Contains("ProductName") && r["ProductName"] != DBNull.Value ? r["ProductName"].ToString() ?? "" : "",
                    CategoryName = r.Table.Columns.Contains("CategoryName") && r["CategoryName"] != DBNull.Value ? r["CategoryName"].ToString() : null,
                    SupplierName = r.Table.Columns.Contains("SupplierName") && r["SupplierName"] != DBNull.Value ? r["SupplierName"].ToString() : null,
                    Price = r.Table.Columns.Contains("Price") && r["Price"] != DBNull.Value ? Convert.ToDecimal(r["Price"]) : 0m,
                    StockQuantity = r.Table.Columns.Contains("StockQuantity") && r["StockQuantity"] != DBNull.Value ? Convert.ToInt32(r["StockQuantity"]) : 0,
                    LowStockThreshold = r.Table.Columns.Contains("LowStockThreshold") && r["LowStockThreshold"] != DBNull.Value ? Convert.ToInt32(r["LowStockThreshold"]) : 10,
                    SKU = r.Table.Columns.Contains("SKU") && r["SKU"] != DBNull.Value ? r["SKU"].ToString() : null,
                    HSNCode = r.Table.Columns.Contains("HSNCode") && r["HSNCode"] != DBNull.Value ? r["HSNCode"].ToString() : null,
                    TaxRate = r.Table.Columns.Contains("TaxRate") && r["TaxRate"] != DBNull.Value ? Convert.ToDecimal(r["TaxRate"]) : 0m,
                    ImageUrl = r.Table.Columns.Contains("ImageUrl") && r["ImageUrl"] != DBNull.Value ? r["ImageUrl"].ToString() : null,
                    IsPublished = r.Table.Columns.Contains("IsPublished") && r["IsPublished"] != DBNull.Value ? Convert.ToBoolean(r["IsPublished"]) : true,
                    StockStatus = r.Table.Columns.Contains("StockStatus") && r["StockStatus"] != DBNull.Value ? r["StockStatus"].ToString() : "Unknown",
                    IsDeleted = r.Table.Columns.Contains("IsDeleted") && r["IsDeleted"] != DBNull.Value ? Convert.ToBoolean(r["IsDeleted"]) : false,
                    ImageData = r.Table.Columns.Contains("ImageData") && r["ImageData"] != DBNull.Value ? (byte[])r["ImageData"] : null,
                    ImageMimeType = r.Table.Columns.Contains("ImageMimeType") && r["ImageMimeType"] != DBNull.Value ? r["ImageMimeType"].ToString() : null
                });
            }
            return list;
        }

        public Product? GetById(int productId, int companyId)
        {
            var dt = _db.ExecuteQuery("sp_GetProductById",
                DbHelper.P("@CompanyId", companyId),
                DbHelper.P("@ProductId", productId));

            if (dt.Rows.Count > 0)
            {
                var r = dt.Rows[0];
                return new Product
                {
                    ProductId = r.Table.Columns.Contains("ProductId") && r["ProductId"] != DBNull.Value ? Convert.ToInt32(r["ProductId"]) : 0,
                    CompanyId = r.Table.Columns.Contains("CompanyId") && r["CompanyId"] != DBNull.Value ? Convert.ToInt32(r["CompanyId"]) : 0,
                    CategoryId = r.Table.Columns.Contains("CategoryId") && r["CategoryId"] != DBNull.Value ? Convert.ToInt32(r["CategoryId"]) : null,
                    SupplierId = r.Table.Columns.Contains("SupplierId") && r["SupplierId"] != DBNull.Value ? Convert.ToInt32(r["SupplierId"]) : null,
                    ProductName = r.Table.Columns.Contains("ProductName") && r["ProductName"] != DBNull.Value ? r["ProductName"].ToString() ?? "" : "",
                    CategoryName = r.Table.Columns.Contains("CategoryName") && r["CategoryName"] != DBNull.Value ? r["CategoryName"].ToString() : null,
                    SupplierName = r.Table.Columns.Contains("SupplierName") && r["SupplierName"] != DBNull.Value ? r["SupplierName"].ToString() : null,
                    Price = r.Table.Columns.Contains("Price") && r["Price"] != DBNull.Value ? Convert.ToDecimal(r["Price"]) : 0m,
                    StockQuantity = r.Table.Columns.Contains("StockQuantity") && r["StockQuantity"] != DBNull.Value ? Convert.ToInt32(r["StockQuantity"]) : 0,
                    LowStockThreshold = r.Table.Columns.Contains("LowStockThreshold") && r["LowStockThreshold"] != DBNull.Value ? Convert.ToInt32(r["LowStockThreshold"]) : 10,
                    SKU = r.Table.Columns.Contains("SKU") && r["SKU"] != DBNull.Value ? r["SKU"].ToString() : null,
                    HSNCode = r.Table.Columns.Contains("HSNCode") && r["HSNCode"] != DBNull.Value ? r["HSNCode"].ToString() : null,
                    TaxRate = r.Table.Columns.Contains("TaxRate") && r["TaxRate"] != DBNull.Value ? Convert.ToDecimal(r["TaxRate"]) : 0m,
                    ImageUrl = r.Table.Columns.Contains("ImageUrl") && r["ImageUrl"] != DBNull.Value ? r["ImageUrl"].ToString() : null,
                    IsPublished = r.Table.Columns.Contains("IsPublished") && r["IsPublished"] != DBNull.Value ? Convert.ToBoolean(r["IsPublished"]) : true,
                    StockStatus = r.Table.Columns.Contains("StockStatus") && r["StockStatus"] != DBNull.Value ? r["StockStatus"].ToString() : "Unknown",
                    IsDeleted = r.Table.Columns.Contains("IsDeleted") && r["IsDeleted"] != DBNull.Value ? Convert.ToBoolean(r["IsDeleted"]) : false,
                    ImageData = r.Table.Columns.Contains("ImageData") && r["ImageData"] != DBNull.Value ? (byte[])r["ImageData"] : null,
                    ImageMimeType = r.Table.Columns.Contains("ImageMimeType") && r["ImageMimeType"] != DBNull.Value ? r["ImageMimeType"].ToString() : null
                };
            }
            return null;
        }

        public void Insert(Product product)
        {
            if (product.CreatedBy == null || product.CreatedBy <= 0)
                throw new ArgumentException("CreatedBy (UserId) is required for auditing purposes in a SaaS environment.");

            _db.ExecuteNonQuery("sp_UpsertProduct",
                DbHelper.P("@CompanyId", product.CompanyId),
                DbHelper.P("@ProductId", 0),
                DbHelper.P("@CategoryId", product.CategoryId),
                DbHelper.P("@SupplierId", product.SupplierId),
                DbHelper.P("@ProductName", product.ProductName),
                DbHelper.P("@Price", product.Price),
                DbHelper.P("@StockQuantity", product.StockQuantity),
                DbHelper.P("@LowStockThreshold", product.LowStockThreshold),
                DbHelper.P("@UserId", product.CreatedBy),
                DbHelper.P("@ImageData", product.ImageData ?? (object)DBNull.Value, SqlDbType.VarBinary),
                DbHelper.P("@ImageMimeType", product.ImageMimeType ?? (object)DBNull.Value)
            );
            _db.LogAudit(product.CompanyId, "Products", "INSERT", $"Added product: {product.ProductName}");
        }

        public void Update(Product product)
        {
            if (product.CreatedBy == null || product.CreatedBy <= 0)
                throw new ArgumentException("CreatedBy (UserId) is required for auditing purposes in a SaaS environment.");

            _db.ExecuteNonQuery("sp_UpsertProduct",
                DbHelper.P("@CompanyId", product.CompanyId),
                DbHelper.P("@ProductId", product.ProductId),
                DbHelper.P("@CategoryId", product.CategoryId),
                DbHelper.P("@SupplierId", product.SupplierId),
                DbHelper.P("@ProductName", product.ProductName),
                DbHelper.P("@Price", product.Price),
                DbHelper.P("@StockQuantity", product.StockQuantity),
                DbHelper.P("@LowStockThreshold", product.LowStockThreshold),
                DbHelper.P("@UserId", product.CreatedBy),
                DbHelper.P("@ImageData", product.ImageData ?? (object)DBNull.Value, SqlDbType.VarBinary),
                DbHelper.P("@ImageMimeType", product.ImageMimeType ?? (object)DBNull.Value)
            );
            _db.LogAudit(product.CompanyId, "Products", "UPDATE", $"Updated product: {product.ProductName} (ID: {product.ProductId})");
        }

        public void Delete(int productId, int companyId)
        {
            _db.ExecuteNonQuery("sp_DeleteProduct",
                DbHelper.P("@CompanyId", companyId),
                DbHelper.P("@ProductId", productId)
            );
            _db.LogAudit(companyId, "Products", "DELETE", $"Deleted product ID: {productId}");
        }

        public int UpsertCategory(int companyId, string categoryName)
        {
            var dt = _db.ExecuteQuery("sp_UpsertCategory",
                DbHelper.P("@CompanyId", companyId),
                DbHelper.P("@CategoryId", 0),
                DbHelper.P("@CategoryName", categoryName)
            );
            if (dt.Rows.Count > 0)
            {
                return Convert.ToInt32(dt.Rows[0]["CategoryId"]);
            }
            throw new Exception("Failed to insert category securely using Stored Procedure.");
        }
    }
}