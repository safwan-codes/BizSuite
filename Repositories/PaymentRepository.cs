using System;
using System.Collections.Generic;
using System.Data;
using BizSuite.Models;
using BizSuite.Data;
using Microsoft.Data.SqlClient;

namespace BizSuite.Repositories
{
    public interface IPaymentRepository
    {
        int AddPayment(int companyId, int invoiceId, decimal amount, string method);
        void UpdateInvoiceStatus(int companyId, int invoiceId, string status);
    }

    public class PaymentRepository : IPaymentRepository
    {
        private readonly DbHelper _db;

        public PaymentRepository(DbHelper db)
        {
            _db = db;
        }

        public int AddPayment(int companyId, int invoiceId, decimal amount, string method)
        {
            _db.ExecuteNonQuery("sp_AddPayment",
                DbHelper.P("@CompanyId", companyId),
                DbHelper.P("@InvoiceId", invoiceId),
                DbHelper.P("@AmountPaid", amount),
                DbHelper.P("@Method", method));

            _db.LogAudit(companyId, "Payments", "INSERT", $"Payment of {amount:C} received for Invoice #{invoiceId} via {method}");
            return 1;
        }

        public void UpdateInvoiceStatus(int companyId, int invoiceId, string status)
        {

            _db.ExecuteNonQuery("UPDATE Invoices SET Status = @S WHERE InvoiceId = @I AND CompanyId = @C",
                DbHelper.P("@S", status),
                DbHelper.P("@I", invoiceId),
                DbHelper.P("@C", companyId));
            
            _db.LogAudit(companyId, "Invoices", "UPDATE", $"Invoice #{invoiceId} status manually changed to {status}");
        }
    }
}
