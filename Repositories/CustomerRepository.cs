using System;
using System.Collections.Generic;
using System.Data;
using BizSuite.Models;
using BizSuite.Data;
using Microsoft.Data.SqlClient;

namespace BizSuite.Repositories
{
    public class CustomerRepository : ICustomerRepository
    {
        private readonly DbHelper _db;

        public CustomerRepository(DbHelper db)
        {
            _db = db;
        }

        public IEnumerable<Customer> GetAll(int companyId)
        {
            var dt = _db.ExecuteQuery("sp_GetCustomers", DbHelper.P("@CompanyId", companyId));
            var list = new List<Customer>();
            foreach (DataRow r in dt.Rows)
            {
                list.Add(new Customer
                {
                    CustomerId = Convert.ToInt32(r["CustomerId"]),
                    CompanyId = Convert.ToInt32(r["CompanyId"]),
                    CustomerName = r["CustomerName"].ToString() ?? "",
                    Email = r["Email"] != DBNull.Value ? r["Email"].ToString() : "",
                    Phone = r["Phone"] != DBNull.Value ? r["Phone"].ToString() : "",
                    Address = r["Address"] != DBNull.Value ? r["Address"].ToString() : "",
                    IsDeleted = Convert.ToBoolean(r["IsDeleted"]),
                    Orders = r.Table.Columns.Contains("Orders") && r["Orders"] != DBNull.Value ? Convert.ToInt32(r["Orders"]) : 0,
                    Spend = r.Table.Columns.Contains("Spend") && r["Spend"] != DBNull.Value ? Convert.ToDecimal(r["Spend"]) : 0m,
                    ImageData = r.Table.Columns.Contains("ImageData") && r["ImageData"] != DBNull.Value ? (byte[])r["ImageData"] : null,
                    ImageMimeType = r.Table.Columns.Contains("ImageMimeType") && r["ImageMimeType"] != DBNull.Value ? r["ImageMimeType"].ToString() : null
                });
            }
            return list;
        }

        public Customer? GetById(int customerId, int companyId)
        {
            var dt = _db.ExecuteQuery("sp_GetCustomerById",
                DbHelper.P("@CompanyId", companyId),
                DbHelper.P("@CustomerId", customerId));

            if (dt.Rows.Count > 0)
            {
                var r = dt.Rows[0];
                return new Customer
                {
                    CustomerId = Convert.ToInt32(r["CustomerId"]),
                    CompanyId = Convert.ToInt32(r["CompanyId"]),
                    CustomerName = r["CustomerName"].ToString() ?? "",
                    Email = r["Email"] != DBNull.Value ? r["Email"].ToString() : "",
                    Phone = r["Phone"] != DBNull.Value ? r["Phone"].ToString() : "",
                    Address = r["Address"] != DBNull.Value ? r["Address"].ToString() : "",
                    IsDeleted = Convert.ToBoolean(r["IsDeleted"]),
                    ImageData = r.Table.Columns.Contains("ImageData") && r["ImageData"] != DBNull.Value ? (byte[])r["ImageData"] : null,
                    ImageMimeType = r.Table.Columns.Contains("ImageMimeType") && r["ImageMimeType"] != DBNull.Value ? r["ImageMimeType"].ToString() : null
                };
            }
            return null;
        }

        public void Insert(Customer c)
        {
            _db.ExecuteNonQuery("sp_UpsertCustomer",
                DbHelper.P("@CompanyId", c.CompanyId),
                DbHelper.P("@CustomerId", 0),
                DbHelper.P("@CustomerName", c.CustomerName),
                DbHelper.P("@Email", c.Email),
                DbHelper.P("@Phone", c.Phone),
                DbHelper.P("@Address", c.Address),
                DbHelper.P("@UserId", c.CreatedBy),
                DbHelper.P("@ImageData", c.ImageData ?? (object)DBNull.Value, SqlDbType.VarBinary),
                DbHelper.P("@ImageMimeType", c.ImageMimeType ?? (object)DBNull.Value)
            );
            _db.LogAudit(c.CompanyId, "Customers", "INSERT", $"Created customer: {c.CustomerName}");
        }

        public void Update(Customer c)
        {
            _db.ExecuteNonQuery("sp_UpsertCustomer",
                DbHelper.P("@CompanyId", c.CompanyId),
                DbHelper.P("@CustomerId", c.CustomerId),
                DbHelper.P("@CustomerName", c.CustomerName),
                DbHelper.P("@Email", c.Email),
                DbHelper.P("@Phone", c.Phone),
                DbHelper.P("@Address", c.Address),
                DbHelper.P("@UserId", c.CreatedBy),
                DbHelper.P("@ImageData", c.ImageData ?? (object)DBNull.Value, SqlDbType.VarBinary),
                DbHelper.P("@ImageMimeType", c.ImageMimeType ?? (object)DBNull.Value)
            );
            _db.LogAudit(c.CompanyId, "Customers", "UPDATE", $"Updated customer: {c.CustomerName} (ID: {c.CustomerId})");
        }

        public void Delete(int customerId, int companyId)
        {
            _db.ExecuteNonQuery("sp_DeleteCustomer",
                DbHelper.P("@CompanyId", companyId),
                DbHelper.P("@CustomerId", customerId)
            );
            _db.LogAudit(companyId, "Customers", "DELETE", $"Deleted customer ID: {customerId}");
        }
    }
}