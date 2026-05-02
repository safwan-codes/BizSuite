using Microsoft.AspNetCore.Mvc;
using BizSuite.Repositories;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System;

using Stripe;
using BizSuite.Services;
using Microsoft.Extensions.Configuration;

namespace BizSuite.Controllers
{
    [Authorize]
    public class PaymentController : Controller
    {
        private readonly IPaymentRepository _paymentRepo;
        private readonly ISalesRepository _salesRepo;
        private readonly IShippingService _shippingService;
        private readonly IConfiguration _config;

        public PaymentController(IPaymentRepository paymentRepo, ISalesRepository salesRepo, IShippingService shippingService, IConfiguration config)
        {
            _paymentRepo = paymentRepo;
            _salesRepo = salesRepo;
            _shippingService = shippingService;
            _config = config;
        }

        public IActionResult Checkout(int id)
        {
            if (id <= 0)
                return RedirectToAction("Index", "Staff");

            int companyId = int.Parse(User.FindFirst("CompanyId")?.Value ?? "0");

            int targetSalesOrderId = id;
            var order = _salesRepo.GetOrderDetail(companyId, targetSalesOrderId);

            if (order == null || order.Header.OrderId <= 0)
            {
                var db = HttpContext.RequestServices.GetRequiredService<BizSuite.Data.DbHelper>();
                var dt = db.ExecuteQuery("SELECT SalesOrderId FROM Invoices WHERE InvoiceId = @I AND CompanyId = @C",
                    BizSuite.Data.DbHelper.P("@I", id), BizSuite.Data.DbHelper.P("@C", companyId));
                
                if (dt.Rows.Count > 0)
                {
                    targetSalesOrderId = Convert.ToInt32(dt.Rows[0]["SalesOrderId"]);
                    order = _salesRepo.GetOrderDetail(companyId, targetSalesOrderId);
                }
            }

            if (order == null || order.Header.OrderId <= 0)
            {
                ViewBag.ErrorMessage = $"Transaction #{id} could not be resolved to a valid Order. It may belong to another company or has been deleted.";
                return View("Error");
            }

            // Stripe PaymentIntent creation
            if (_config["Stripe:SecretKey"] == "sk_test_placeholder_key_here")
            {
                ViewBag.ClientSecret = "pi_mock_123_secret_mock456";
            }
            else
            {
                try
                {
                    var options = new PaymentIntentCreateOptions
                    {
                        Amount = (long)(order.Header.Total * 100),
                        Currency = "inr",
                        AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions { Enabled = true },
                        Metadata = new Dictionary<string, string>
                        {
                            { "OrderId", order.Header.OrderId.ToString() },
                            { "CompanyId", companyId.ToString() }
                        }
                    };
                    var service = new PaymentIntentService();
                    var intent = service.Create(options);
                    ViewBag.ClientSecret = intent.ClientSecret;
                }
                catch (Exception ex)
                {
                    ViewBag.ErrorMessage = "Failed to initialize payment gateway: " + ex.Message;
                    return View("Error");
                }
            }

            ViewBag.StripePublishableKey = _config["Stripe:PublishableKey"];
            return View(order);
        }

        [HttpPost]
        public async Task<IActionResult> Process()
        {
            string jsonBody;
            using (var reader = new System.IO.StreamReader(Request.Body))
            {
                jsonBody = await reader.ReadToEndAsync();
            }

            var req = Newtonsoft.Json.JsonConvert.DeserializeObject<PaymentProcessRequest>(jsonBody);

            if (req == null || req.OrderId <= 0)
                return Json(new { success = false, message = "Invalid request payload. Order ID is missing." });

            int companyId = int.Parse(User.FindFirst("CompanyId")?.Value ?? "0");

            System.Threading.Thread.Sleep(500);

            string transactionId = "TXN" + DateTime.Now.Ticks.ToString().Substring(10);

            var db = HttpContext.RequestServices.GetRequiredService<BizSuite.Data.DbHelper>();
            var dt = db.ExecuteQuery("SELECT InvoiceId, TotalAmount FROM Invoices WHERE SalesOrderId = @O AND CompanyId = @C",
                    BizSuite.Data.DbHelper.P("@O", req.OrderId),
                    BizSuite.Data.DbHelper.P("@C", companyId));

            int actualInvoiceId;
            decimal amount;

            if (dt.Rows.Count == 0) 
            {
                
                var soDt = db.ExecuteQuery("SELECT TotalAmount, OrderDate FROM SalesOrders WHERE SalesOrderId = @O AND CompanyId = @C", 
                            BizSuite.Data.DbHelper.P("@O", req.OrderId), BizSuite.Data.DbHelper.P("@C", companyId));
                
                if (soDt.Rows.Count == 0) 
                {
                    return Json(new { success = false, message = "Sales order not found in database." });
                }

                amount = Convert.ToDecimal(soDt.Rows[0]["TotalAmount"]);
                DateTime orderDate = soDt.Rows[0]["OrderDate"] != DBNull.Value ? Convert.ToDateTime(soDt.Rows[0]["OrderDate"]) : DateTime.Now;

                db.ExecuteNonQuery("INSERT INTO Invoices (CompanyId, SalesOrderId, InvoiceDate, TotalAmount, Status) VALUES (@C, @O, @D, @A, 'Pending')",
                    BizSuite.Data.DbHelper.P("@C", companyId),
                    BizSuite.Data.DbHelper.P("@O", req.OrderId),
                    BizSuite.Data.DbHelper.P("@D", orderDate),
                    BizSuite.Data.DbHelper.P("@A", amount));

                var dtNew = db.ExecuteQuery("SELECT InvoiceId FROM Invoices WHERE SalesOrderId = @O AND CompanyId = @C",
                    BizSuite.Data.DbHelper.P("@O", req.OrderId), BizSuite.Data.DbHelper.P("@C", companyId));
                
                actualInvoiceId = Convert.ToInt32(dtNew.Rows[0]["InvoiceId"]);
            }
            else
            {
                actualInvoiceId = Convert.ToInt32(dt.Rows[0]["InvoiceId"]);
                amount = Convert.ToDecimal(dt.Rows[0]["TotalAmount"]);
            }

            _paymentRepo.AddPayment(companyId, actualInvoiceId, amount, req.Method ?? "Card");

            _paymentRepo.UpdateInvoiceStatus(companyId, actualInvoiceId, "Paid");

            db.ExecuteNonQuery("UPDATE SalesOrders SET Status = 'Paid' WHERE SalesOrderId = @O AND CompanyId = @C",
                BizSuite.Data.DbHelper.P("@O", req.OrderId),
                BizSuite.Data.DbHelper.P("@C", companyId));

            db.AddNotification(companyId, null, "Payment Received", $"A payment of ₹{amount:N0} was successfully received for Order #{req.OrderId}.", "success");

            // == SHIPMENT INTERLOCK ==
            // Once paid successfully, trigger the shipping API to generate a label
            string trackingNumber = await _shippingService.GenerateShippingLabelAsync(companyId, req.OrderId, "Customer", "Auto-determined");
            
            if (!string.IsNullOrEmpty(trackingNumber))
            {
                db.ExecuteNonQuery("UPDATE SalesOrders SET FulfillmentStatus = 'In_Transit', TrackingNumber = @T WHERE SalesOrderId = @O AND CompanyId = @C",
                    BizSuite.Data.DbHelper.P("@T", trackingNumber), BizSuite.Data.DbHelper.P("@O", req.OrderId), BizSuite.Data.DbHelper.P("@C", companyId));
                
                db.AddNotification(companyId, null, "Shipment Created", $"Label generated for Order #{req.OrderId}. Tracking: {trackingNumber}", "info");
            }

            return Json(new { success = true, transactionId });
        }

        public IActionResult Success(string txn)
        {
            ViewBag.TxnId = txn;
            return View();
        }
    }

    public class PaymentProcessRequest
    {
        public int OrderId { get; set; }
        public string Method { get; set; }
        public string PaymentIntentId { get; set; }
    }
}
