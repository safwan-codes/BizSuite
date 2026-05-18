using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using BizSuite.Models.Dashboard;
using BizSuite.Repositories;
using System;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using BizSuite.Data;
using BizSuite.Models;

namespace BizSuite.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly IDashboardRepository _dashboardRepo;
        private readonly ICustomerRepository _customerRepo;
        private readonly IProductRepository _productRepo;
        private readonly ISalesRepository _salesRepo;
        private readonly ILogger<AdminController> _logger;
        private readonly DbHelper _db;

        public AdminController(
            IDashboardRepository dashboardRepo,
            ICustomerRepository customerRepo,
            IProductRepository productRepo,
            ISalesRepository salesRepo,
            ILogger<AdminController> logger,
            DbHelper db)
        {
            _dashboardRepo = dashboardRepo;
            _customerRepo = customerRepo;
            _productRepo = productRepo;
            _salesRepo = salesRepo;
            _logger = logger;
            _db = db;
        }

        public IActionResult Dashboard()
        {
            try
            {
                var vm = _dashboardRepo.GetAdminStats(
                    GetCompanyId(),
                    HttpContext.Session.GetString("FullName") ?? "Admin",
                    HttpContext.Session.GetString("Company") ?? "BizSuite Corp");
                return View(vm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading admin dashboard");
                TempData["Error"] = "Could not load dashboard data.";
                return View(new AdminDashboardViewModel());
            }
        }

        public IActionResult Customers()
        {
            SetViewBag();
            try
            {
                var customers = _customerRepo.GetAll(GetCompanyId());
                return View(customers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading customers");
                TempData["Error"] = "Could not load customers.";
                return View(Enumerable.Empty<BizSuite.Models.Customer>());
            }
        }

        [HttpGet]
        public IActionResult CreateCustomer()
        {
            SetViewBag();
            return View(new BizSuite.Models.CustomerViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateCustomer(BizSuite.Models.CustomerViewModel model)
        {
            SetViewBag();
            if (!ModelState.IsValid) return View(model);
            try
            {
                var c = new BizSuite.Models.Customer
                {
                    CompanyId = GetCompanyId(),
                    CustomerName = model.CustomerName,
                    Email = model.Email,
                    Phone = model.Phone,
                    Address = model.Address,
                    IsDeleted = model.IsDeleted,
                    CreatedBy = HttpContext.Session.GetInt32("UserId") ?? 1
                };

                if (model.ImageFile != null && model.ImageFile.Length > 0)
                {
                    using var ms = new MemoryStream();
                    model.ImageFile.CopyTo(ms);
                    c.ImageData = ms.ToArray();
                    c.ImageMimeType = model.ImageFile.ContentType;
                }

                _customerRepo.Insert(c);
                TempData["Success"] = $"Customer '{c.CustomerName}' added successfully.";
                return RedirectToAction("Customers");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating customer");
                ModelState.AddModelError("", "Could not save customer.");
                return View(model);
            }
        }

        [HttpGet]
        public IActionResult EditCustomer(int id)
        {
            SetViewBag();
            var c = _customerRepo.GetById(id, GetCompanyId());
            if (c == null) return NotFound();
            var model = new BizSuite.Models.CustomerViewModel
            {
                CustomerId = c.CustomerId,
                CustomerName = c.CustomerName,
                Email = c.Email,
                Phone = c.Phone,
                Address = c.Address,
                IsDeleted = c.IsDeleted
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditCustomer(BizSuite.Models.CustomerViewModel model)
        {
            SetViewBag();
            if (!ModelState.IsValid) return View(model);
            try
            {
                var c = new BizSuite.Models.Customer
                {
                    CustomerId = model.CustomerId,
                    CompanyId = GetCompanyId(),
                    CustomerName = model.CustomerName,
                    Email = model.Email,
                    Phone = model.Phone,
                    Address = model.Address,
                    IsDeleted = model.IsDeleted,
                    CreatedBy = HttpContext.Session.GetInt32("UserId") ?? 1
                };

                if (model.ImageFile != null && model.ImageFile.Length > 0)
                {
                    using var ms = new MemoryStream();
                    model.ImageFile.CopyTo(ms);
                    c.ImageData = ms.ToArray();
                    c.ImageMimeType = model.ImageFile.ContentType;
                }

                _customerRepo.Update(c);
                TempData["Success"] = $"Customer '{c.CustomerName}' updated.";
                return RedirectToAction("Customers");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating customer");
                ModelState.AddModelError("", "Could not update customer.");
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteCustomer(int customerId)
        {
            try
            {
                _customerRepo.Delete(customerId, GetCompanyId());
                TempData["Success"] = "Customer deleted successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting customer {Id}", customerId);
                TempData["Error"] = "Could not delete customer.";
            }
            return RedirectToAction("Customers");
        }

        public IActionResult Products()
        {
            SetViewBag();
            try
            {
                var products = _productRepo.GetAll(GetCompanyId());
                return View(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading products");
                TempData["Error"] = "Could not load products.";
                return View(new List<BizSuite.Models.Product>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddProductAjax(int productId, string productName, string category, decimal price, int stock, int threshold, IFormFile imageFile)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(productName))
                    return Json(new { success = false, message = "Product name is required." });
                if (price <= 0)
                    return Json(new { success = false, message = "Price must be greater than 0." });

                int companyId = GetCompanyId();
                int userId = HttpContext.Session.GetInt32("UserId") ?? 1;

                string catName = string.IsNullOrWhiteSpace(category) ? "Default" : category.Trim();
                int assignedCategoryId = _productRepo.UpsertCategory(companyId, catName);

                var p = new BizSuite.Models.Product
                {
                    ProductId = productId,
                    CompanyId = companyId,
                    ProductName = productName,
                    CategoryId = assignedCategoryId,
                    Price = price,
                    StockQuantity = stock,
                    LowStockThreshold = threshold,
                    CreatedBy = userId,
                    IsDeleted = false
                };

                if (imageFile != null && imageFile.Length > 0)
                {
                    using var ms = new MemoryStream();
                    imageFile.CopyTo(ms);
                    p.ImageData = ms.ToArray();
                    p.ImageMimeType = imageFile.ContentType;
                }

                if (productId == 0)
                    _productRepo.Insert(p);
                else
                    _productRepo.Update(p);

                return Json(new { success = true, message = $"Product '{productName}' saved successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AJAX add product error");
                string realError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = "DB Error: " + realError });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteProductAjax(int productId)
        {
            try
            {
                _productRepo.Delete(productId, GetCompanyId());
                return Json(new { success = true, message = $"Product #{productId} deleted." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AJAX delete product error for #{ProductId}", productId);
                return Json(new { success = false, message = "Server error. Please try again." });
            }
        }

        public IActionResult SalesOrders([FromQuery] SalesOrderQueryParameters parameters)
        {
            SetViewBag();
            try
            {
                parameters ??= new SalesOrderQueryParameters();
                var pagedOrders = _salesRepo.GetSalesOrdersPaged(GetCompanyId(), parameters);
                ViewBag.QueryParams = parameters;
                return View(pagedOrders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading sales orders");
                TempData["Error"] = "Could not load orders.";
                return View(new PagedResult<BizSuite.Models.SalesOrderRow>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult BulkProcessOrders([FromBody] List<int> orderIds)
        {
            try
            {
                if (orderIds == null || orderIds.Count == 0) 
                    return Json(new { success = false, message = "No orders selected." });
                
                int companyId = GetCompanyId();
                int successCount = 0;

                foreach(var id in orderIds)
                {
                    // Simulated bulk transition from Pending -> In_Transit, mimicking a bulk label print event
                    _salesRepo.UpdateFulfillmentStatus(companyId, id, "In_Transit", "Bulk_Post", "BLK_" + DateTime.Now.Ticks);
                    successCount++;
                }

                return Json(new { success = true, message = $"Successfully processed {successCount} orders." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in bulk processing orders");
                return Json(new { success = false, message = "Failed to process orders. " + ex.Message });
            }
        }

        public IActionResult ViewOrder(int id)
        {
            SetViewBag();
            int companyId = GetCompanyId();
            
            string headerSql = @"
                SELECT s.SalesOrderId, c.CustomerName, s.OrderDate, s.TotalAmount, s.FulfillmentStatus
                FROM SalesOrders s
                JOIN Customers c ON s.CustomerId = c.CustomerId
                WHERE s.SalesOrderId = @Id AND s.CompanyId = @C";
            var headerDt = _db.ExecuteQuery(headerSql, DbHelper.P("@Id", id), DbHelper.P("@C", companyId));
            if (headerDt.Rows.Count == 0) return NotFound("Order not found.");
            
            var headRow = headerDt.Rows[0];
            
            string itemsSql = @"
                SELECT p.ProductName, oi.Quantity, oi.UnitPrice, oi.LineTotal
                FROM OrderItems oi
                JOIN Products p ON oi.ProductId = p.ProductId
                WHERE oi.SalesOrderId = @OrderId AND oi.CompanyId = @C";
            var itemsDt = _db.ExecuteQuery(itemsSql, DbHelper.P("@OrderId", id), DbHelper.P("@C", companyId));
            
            var itemsList = new List<OrderItemDetail>();
            foreach (System.Data.DataRow itemRow in itemsDt.Rows)
            {
                itemsList.Add(new OrderItemDetail
                {
                    ProductName = itemRow["ProductName"].ToString(),
                    Quantity = Convert.ToInt32(itemRow["Quantity"]),
                    UnitPrice = Convert.ToDecimal(itemRow["UnitPrice"]),
                    LineTotal = Convert.ToDecimal(itemRow["LineTotal"])
                });
            }

            string invSql = "SELECT TOP 1 InvoiceId, Status FROM Invoices WHERE SalesOrderId = @Id AND CompanyId = @C";
            var invDt = _db.ExecuteQuery(invSql, DbHelper.P("@Id", id), DbHelper.P("@C", companyId));
            InvoiceRow invoiceRow = null;
            if (invDt.Rows.Count > 0)
            {
                invoiceRow = new InvoiceRow 
                { 
                    InvoiceId = Convert.ToInt32(invDt.Rows[0]["InvoiceId"]),
                    Status = invDt.Rows[0]["Status"].ToString()
                };
            }

            var vm = new OrderDetailViewModel
            {
                Header = new SalesOrderRow
                {
                    OrderId = Convert.ToInt32(headRow["SalesOrderId"]),
                    Customer = headRow["CustomerName"].ToString(),
                    OrderDate = headRow["OrderDate"] != DBNull.Value ? Convert.ToDateTime(headRow["OrderDate"]).ToString("dd MMM yyyy HH:mm") : "",
                    Total = Convert.ToDecimal(headRow["TotalAmount"]),
                    Status = headRow["FulfillmentStatus"].ToString(),
                    Items = itemsList.Count
                },
                Items = itemsList,
                Invoice = invoiceRow
            };

            return View(vm);
        }

        public IActionResult Invoices()
        {
            SetViewBag();
            try
            {
                var invoices = _salesRepo.GetInvoices(GetCompanyId());
                return View(invoices);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading invoices");
                TempData["Error"] = "Could not load invoices.";
                return View(new List<BizSuite.Models.InvoiceRow>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddInvoiceAjax(string customer, decimal amount, string status, string method)
        {
            try
            {
                int companyId = GetCompanyId();
                int userId = HttpContext.Session.GetInt32("UserId") ?? 1;

                int customerId = 0;
                var custObj = _db.ExecuteScalar("SELECT TOP 1 CustomerId FROM Customers WHERE CompanyId = @C AND CustomerName = @N", DbHelper.P("@C", companyId), DbHelper.P("@N", customer));
                
                if (custObj != null && custObj != DBNull.Value)
                {
                    customerId = Convert.ToInt32(custObj);
                }
                else
                {
                    _db.ExecuteNonQuery("INSERT INTO Customers (CompanyId, CustomerName, CreatedBy) VALUES (@C, @N, @U)",
                        DbHelper.P("@C", companyId), DbHelper.P("@N", customer), DbHelper.P("@U", userId));
                    customerId = Convert.ToInt32(_db.ExecuteScalar("SELECT SCOPE_IDENTITY()"));
                }

                _db.ExecuteNonQuery("INSERT INTO SalesOrders (CompanyId, CustomerId, OrderDate, TotalAmount, CreatedBy, InvoiceStatus, FulfillmentStatus) VALUES (@C, @Cust, GETDATE(), @Amt, @U, @Stat, 'Delivered')",
                    DbHelper.P("@C", companyId), DbHelper.P("@Cust", customerId), DbHelper.P("@Amt", amount), DbHelper.P("@U", userId), DbHelper.P("@Stat", status));
                int orderId = Convert.ToInt32(_db.ExecuteScalar("SELECT SCOPE_IDENTITY()"));

                _db.ExecuteNonQuery("INSERT INTO Invoices (CompanyId, SalesOrderId, InvoiceDate, TotalAmount, Status, CreatedBy) VALUES (@C, @O, GETDATE(), @Amt, @Stat, @U)",
                    DbHelper.P("@C", companyId), DbHelper.P("@O", orderId), DbHelper.P("@Amt", amount), DbHelper.P("@Stat", status), DbHelper.P("@U", userId));
                int invoiceId = Convert.ToInt32(_db.ExecuteScalar("SELECT SCOPE_IDENTITY()"));

                if (status == "Paid" && !string.IsNullOrEmpty(method) && method != "None")
                {
                    _db.ExecuteNonQuery("sp_AddPayment",
                        DbHelper.P("@CompanyId", companyId),
                        DbHelper.P("@InvoiceId", invoiceId),
                        DbHelper.P("@AmountPaid", amount),
                        DbHelper.P("@Method", method));
                }

                return Json(new { success = true, invoiceId = invoiceId, message = "Invoice created successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating manual invoice");
                return Json(new { success = false, message = "Server error adding invoice: " + ex.Message });
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult GetCustomerImage(int id)
        {
            var customer = _customerRepo.GetById(id, GetCompanyId());
            if (customer != null && customer.ImageData != null && !string.IsNullOrEmpty(customer.ImageMimeType))
            {
                return File(customer.ImageData, customer.ImageMimeType);
            }

            string initial = "C";
            if (customer != null && !string.IsNullOrWhiteSpace(customer.CustomerName))
            {
                initial = customer.CustomerName.Trim().Substring(0, 1).ToUpper();
            }

            string svg = $@"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'><rect width='100' height='100' fill='#2b3035'/><text x='50' y='50' dy='.35em' fill='#fff' font-family='Arial, sans-serif' font-size='50' font-weight='bold' text-anchor='middle'>{initial}</text></svg>";
            return File(System.Text.Encoding.UTF8.GetBytes(svg), "image/svg+xml");
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult GetProductImage(int id)
        {
            var product = _productRepo.GetById(id, GetCompanyId());
            if (product != null && product.ImageData != null && !string.IsNullOrEmpty(product.ImageMimeType))
                return File(product.ImageData, product.ImageMimeType);

            // Dynamic SVG placeholder — no static file dependency
            string initial = "P";
            if (product != null && !string.IsNullOrWhiteSpace(product.ProductName))
                initial = product.ProductName.Trim().Substring(0, 1).ToUpper();

            string svg = $"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'><rect width='100' height='100' fill='#143D34'/><text x='50' y='50' dy='.35em' fill='#C9F76F' font-family='Arial, sans-serif' font-size='50' font-weight='bold' text-anchor='middle'>{initial}</text></svg>";
            return File(System.Text.Encoding.UTF8.GetBytes(svg), "image/svg+xml");
        }

        // ── Audit Logs ─────────────────────────────────────────
        public IActionResult AuditLogs()
        {
            SetViewBag();
            int companyId = GetCompanyId();
            var dt = _db.ExecuteQuery("SELECT TOP 500 * FROM AuditLogs WHERE CompanyId = @C ORDER BY ActionDate DESC", DbHelper.P("@C", companyId));
            var logs = new List<dynamic>();
            foreach (System.Data.DataRow r in dt.Rows)
            {
                int logId = 0;
                if (dt.Columns.Contains("LogId") && r["LogId"] != DBNull.Value) logId = Convert.ToInt32(r["LogId"]);
                else if (dt.Columns.Contains("Id") && r["Id"] != DBNull.Value) logId = Convert.ToInt32(r["Id"]);

                logs.Add(new
                {
                    LogId = logId,
                    TableName = dt.Columns.Contains("TableName") ? r["TableName"].ToString() : "Unknown",
                    ActionType = dt.Columns.Contains("ActionType") ? r["ActionType"].ToString() : "Unknown",
                    ActionDate = dt.Columns.Contains("ActionDate") && r["ActionDate"] != DBNull.Value ? Convert.ToDateTime(r["ActionDate"]).ToString("dd MMM yyyy HH:mm") : "",
                    Details = dt.Columns.Contains("Details") ? r["Details"].ToString() : "",
                    IPAddress = dt.Columns.Contains("IPAddress") && r["IPAddress"] != DBNull.Value ? r["IPAddress"]?.ToString() ?? "—" : "—"
                });
            }
            return View(logs);
        }

        public IActionResult Reports()
        {
            SetViewBag();
            try
            {
                var vm = _dashboardRepo.GetAdminStats(
                    GetCompanyId(),
                    HttpContext.Session.GetString("FullName") ?? "Admin",
                    HttpContext.Session.GetString("Company") ?? "BizSuite Corp");
                return View(vm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading admin reports");
                TempData["Error"] = "Could not load report data.";
                return View(new AdminDashboardViewModel());
            }
        }

        public IActionResult Settings()
        {
            SetViewBag();
            int companyId = GetCompanyId();
            var model = new BizSuite.Models.SettingsViewModel { CompanyId = companyId };

            var dt = _db.ExecuteQuery("SELECT * FROM Companies WHERE CompanyId = @C", DbHelper.P("@C", companyId));
            if (dt.Rows.Count > 0)
            {
                var r = dt.Rows[0];
                model.CompanyName = r["CompanyName"].ToString() ?? "";
                if (dt.Columns.Contains("SubscriptionPlan"))
                    model.SubscriptionPlan = r["SubscriptionPlan"].ToString() ?? "Free";
                else if (dt.Columns.Contains("SubscriptionTier"))
                    model.SubscriptionPlan = r["SubscriptionTier"].ToString() ?? "Free";
                else
                    model.SubscriptionPlan = "Free";
                model.GstNumber = dt.Columns.Contains("GSTIN") && r["GSTIN"] != DBNull.Value ? r["GSTIN"].ToString() : "";
            }

            model.AdminName = ViewBag.FullName;
            model.AdminEmail = ViewBag.Email;

            var staffRepoList = HttpContext.RequestServices.GetService(typeof(IStaffRepository)) as IStaffRepository;
            if (staffRepoList != null)
            {
                model.TeamMembers = staffRepoList.GetAll(companyId).ToList();
            }

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> UploadProfileImage(IFormFile profileImage)
        {
            try
            {
                if (profileImage == null || profileImage.Length == 0)
                    return Json(new { success = false, message = "No image provided." });
                if (profileImage.Length > 2 * 1024 * 1024)
                    return Json(new { success = false, message = "Image is too large. Max size is 2MB." });

                using var ms = new MemoryStream();
                await profileImage.CopyToAsync(ms);
                byte[] imageBytes = ms.ToArray();
                string mimeType = profileImage.ContentType;

                int userId = HttpContext.Session.GetInt32("UserId") ?? 0;
                int companyId = GetCompanyId();
                if (userId == 0) return Json(new { success = false, message = "Unauthorized." });

                _db.ExecuteNonQuery(
                    "UPDATE Users SET ProfileImageData = @Data, ProfileImageMimeType = @Mime WHERE UserId = @UserId AND CompanyId = @CompanyId",
                    DbHelper.P("@Data", imageBytes), DbHelper.P("@Mime", mimeType), DbHelper.P("@UserId", userId), DbHelper.P("@CompanyId", companyId)
                );

                string base64Image = Convert.ToBase64String(imageBytes);
                string imageSrc = $"data:{mimeType};base64,{base64Image}";
                HttpContext.Session.SetString("ProfileImage", imageSrc);

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading profile image.");
                return Json(new { success = false, message = "System error during upload." });
            }
        }

        [HttpPost]
        public IActionResult RemoveProfileImage()
        {
            try
            {
                int userId = HttpContext.Session.GetInt32("UserId") ?? 0;
                int companyId = GetCompanyId();
                if (userId == 0) return Json(new { success = false, message = "Unauthorized." });

                _db.ExecuteNonQuery(
                    "UPDATE Users SET ProfileImageData = NULL, ProfileImageMimeType = NULL WHERE UserId = @UserId AND CompanyId = @CompanyId",
                    DbHelper.P("@UserId", userId), DbHelper.P("@CompanyId", companyId)
                );
                HttpContext.Session.Remove("ProfileImage");
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing profile image.");
                return Json(new { success = false, message = "System error." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> InviteStaffAjax(string fullName, string email, string role, string department)
        {
            try
            {
                var staffRepo = HttpContext.RequestServices.GetService(typeof(IStaffRepository)) as IStaffRepository;
                var emailService = HttpContext.RequestServices.GetService(typeof(BizSuite.Services.IEmailService)) as BizSuite.Services.IEmailService;

                if (staffRepo == null) return Json(new { success = false, message = "Staff repository not found." });

                var staff = new BizSuite.Models.Staff.StaffMember
                {
                    CompanyId = GetCompanyId(),
                    FullName = fullName,
                    Email = email,
                    Role = role,
                    Department = department,
                    IsActive = true
                };
                
                // Insert into database (default password is set in repo: Bizsuite@123)
                staffRepo.Insert(staff);

                // Send actual invitation email
                if (emailService != null)
                {
                    string companyName = HttpContext.Session.GetString("Company") ?? "BizSuite";
                    string subject = $"You have been invited to join {companyName} on BizSuite!";
                    string body = $@"
                        <div style='font-family: Arial, sans-serif; padding: 20px; color: #333;'>
                            <h2>Welcome to {companyName}!</h2>
                            <p>Hi {fullName},</p>
                            <p>You have been invited to join <strong>{companyName}</strong> as a <strong>{role}</strong>.</p>
                            <p>You can now log in to the ERP portal using this email address and your temporary password:</p>
                            <p style='background: #f4f4f4; padding: 10px; font-weight: bold; border-radius: 5px; width: max-content;'>Bizsuite@123</p>
                            <p>Please change your password immediately after logging in.</p>
                            <br/>
                            <p>Best regards,<br/>The BizSuite Team</p>
                        </div>
                    ";

                    await emailService.SendEmailAsync(email, subject, body);
                }

                return Json(new { success = true, message = $"Invitation sent to {fullName} ({email})." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inviting staff");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult DeleteStaffAjax(int staffId)
        {
            try
            {
                var staffRepo = HttpContext.RequestServices.GetService(typeof(IStaffRepository)) as IStaffRepository;
                if (staffRepo == null) return Json(new { success = false, message = "Staff repository not found." });

                staffRepo.Delete(staffId, GetCompanyId());
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting staff");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult GetNotifications()
        {
            int userId = HttpContext.Session.GetInt32("UserId") ?? 1;
            int companyId = GetCompanyId();

            var dt = _db.ExecuteQuery(
                "SELECT TOP 10 * FROM Notifications WHERE CompanyId = @C AND (UserId = @U OR UserId IS NULL) AND IsRead = 0 ORDER BY CreatedDate DESC",
                DbHelper.P("@C", companyId), DbHelper.P("@U", userId)
            );

            var list = new List<dynamic>();
            foreach (System.Data.DataRow r in dt.Rows)
            {
                list.Add(new
                {
                    Id = Convert.ToInt32(r["NotificationId"]),
                    Title = r["Title"].ToString(),
                    Message = r["Message"].ToString(),
                    Type = r["Type"].ToString(),
                    Time = Convert.ToDateTime(r["CreatedDate"]).ToString("dd MMM HH:mm")
                });
            }
            return Json(list);
        }

        [HttpPost]
        public IActionResult MarkNotificationRead(int id)
        {
            int userId = HttpContext.Session.GetInt32("UserId") ?? 1;
            int companyId = GetCompanyId();

            if (id == 0)
            {
                _db.ExecuteNonQuery("UPDATE Notifications SET IsRead = 1 WHERE CompanyId = @C AND (UserId = @U OR UserId IS NULL)",
                    DbHelper.P("@C", companyId), DbHelper.P("@U", userId));
            }
            else
            {
                _db.ExecuteNonQuery("UPDATE Notifications SET IsRead = 1 WHERE NotificationId = @Id", DbHelper.P("@Id", id));
            }

            return Json(new { success = true });
        }

        public IActionResult PrintInvoice(int id)
        {
            SetViewBag();
            int companyId = GetCompanyId();
            string headerSql = @"
                SELECT i.InvoiceId, i.InvoiceDate, c.CustomerName, i.TotalAmount, i.Status, i.SalesOrderId
                FROM Invoices i
                JOIN SalesOrders s ON i.SalesOrderId = s.SalesOrderId
                JOIN Customers c ON s.CustomerId = c.CustomerId
                WHERE i.InvoiceId = @Id AND i.CompanyId = @C";

            var headerDt = _db.ExecuteQuery(headerSql, DbHelper.P("@Id", id), DbHelper.P("@C", companyId));
            if (headerDt.Rows.Count == 0) return NotFound("Invoice not found.");

            var row = headerDt.Rows[0];
            int salesOrderId = Convert.ToInt32(row["SalesOrderId"]);

            string itemsSql = @"
                SELECT p.ProductName, oi.Quantity, oi.UnitPrice, oi.LineTotal
                FROM OrderItems oi
                JOIN Products p ON oi.ProductId = p.ProductId
                WHERE oi.SalesOrderId = @OrderId AND oi.CompanyId = @C";

            var itemsDt = _db.ExecuteQuery(itemsSql, DbHelper.P("@OrderId", salesOrderId), DbHelper.P("@C", companyId));
            var itemsList = new List<dynamic>();
            foreach (System.Data.DataRow itemRow in itemsDt.Rows)
            {
                itemsList.Add(new
                {
                    Name = itemRow["ProductName"].ToString(),
                    Qty = Convert.ToInt32(itemRow["Quantity"]),
                    Price = Convert.ToDecimal(itemRow["UnitPrice"]),
                    Total = Convert.ToDecimal(itemRow["LineTotal"])
                });
            }

            var invoice = new
            {
                InvoiceId = Convert.ToInt32(row["InvoiceId"]),
                InvoiceDate = row["InvoiceDate"] != DBNull.Value ? Convert.ToDateTime(row["InvoiceDate"]).ToString("dd MMM yyyy") : "",
                Customer = row["CustomerName"].ToString() ?? "Unknown",
                Amount = row["TotalAmount"] != DBNull.Value ? Convert.ToDecimal(row["TotalAmount"]) : 0m,
                Status = row["Status"].ToString() ?? "Pending",
                Method = "Cash/Card",
                Items = itemsList
            };
            return View(invoice);
        }

        public IActionResult Search(string q)
        {
            SetViewBag();
            if (string.IsNullOrWhiteSpace(q)) return RedirectToAction("Dashboard");

            var dt = _db.ExecuteQuery("sp_GlobalSearch", DbHelper.P("@CompanyId", GetCompanyId()), DbHelper.P("@Query", q));
            var results = new List<SearchResultViewModel>();
            foreach (System.Data.DataRow r in dt.Rows)
            {
                results.Add(new SearchResultViewModel
                {
                    Source = r["Source"].ToString() ?? "",
                    Id = Convert.ToInt32(r["Id"]),
                    Title = r["Title"].ToString() ?? "",
                    Subtitle = r["Subtitle"].ToString() ?? "",
                    Details = r["Details"].ToString() ?? ""
                });
            }
            ViewBag.SearchQuery = q;
            return View(results);
        }

        public IActionResult ExportCustomers()
        {
            var data = _customerRepo.GetAll(GetCompanyId());
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("CustomerId,CustomerName,Email,Phone,Address,Orders,TotalSpend,Status");
            foreach (var item in data)
                csv.AppendLine($"{item.CustomerId},\"{item.CustomerName}\",\"{item.Email}\",\"{item.Phone}\",\"{item.Address}\",{item.Orders},{item.Spend},{(item.IsDeleted ? "Inactive" : "Active")}");
            return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"Customers_{DateTime.Now:yyyyMMdd}.csv");
        }

        public IActionResult ExportProducts()
        {
            var data = _productRepo.GetAll(GetCompanyId());
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("ProductId,ProductName,Category,Price,Stock,Threshold");
            foreach (var item in data)
                csv.AppendLine($"{item.ProductId},\"{item.ProductName}\",\"{item.CategoryName}\",{item.Price},{item.StockQuantity},{item.LowStockThreshold}");
            return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"Inventory_{DateTime.Now:yyyyMMdd}.csv");
        }

        public IActionResult ExportSalesOrders()
        {
            var data = _salesRepo.GetSalesOrders(GetCompanyId());
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("OrderId,Customer,OrderDate,Total,Status");
            foreach (var item in data)
                csv.AppendLine($"{item.OrderId},\"{item.Customer}\",\"{item.OrderDate}\",{item.Total},\"{item.Status}\"");
            return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"SalesOrders_{DateTime.Now:yyyyMMdd}.csv");
        }

        public IActionResult ExportInvoices()
        {
            var data = _salesRepo.GetInvoices(GetCompanyId());
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("InvoiceId,Customer,InvoiceDate,Amount,Status,Method");
            foreach (var item in data)
                csv.AppendLine($"{item.InvoiceId},\"{item.Customer}\",\"{item.InvoiceDate}\",{item.Amount},\"{item.Status}\",\"{item.Method}\"");
            return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"Invoices_{DateTime.Now:yyyyMMdd}.csv");
        }

        public IActionResult DownloadReport(string type = "monthly", string date = "")
        {
            DateTime refDate = DateTime.TryParse(date, out var d) ? d : DateTime.Now;
            DateTime startDate, endDate;
            string periodLabel;

            if (type.ToLower() == "weekly")
            {
                startDate = refDate.AddDays(-(int)refDate.DayOfWeek);
                endDate = startDate.AddDays(6).AddHours(23).AddMinutes(59).AddSeconds(59);
                periodLabel = $"{startDate:dd MMM} - {endDate:dd MMM yyyy}";
            }
            else
            {
                startDate = new DateTime(refDate.Year, refDate.Month, 1);
                endDate = startDate.AddMonths(1).AddSeconds(-1);
                periodLabel = startDate.ToString("MMMM yyyy");
            }

            int companyId = GetCompanyId();
            string fullName = HttpContext.Session.GetString("FullName") ?? "Admin";
            string company = HttpContext.Session.GetString("Company") ?? "BizSuite Corp";

            var vm = _dashboardRepo.GetAdminStats(companyId, fullName, company, startDate, endDate);
            vm.ReportPeriod = periodLabel;
            vm.ReportType = type.ToUpper() + " SUMMARY REPORT";

            return View("DownloadReport", vm);
        }

        private void SetViewBag()
        {
            ViewBag.FullName = HttpContext.Session.GetString("FullName") ?? "Admin";
            ViewBag.CompanyName = HttpContext.Session.GetString("Company") ?? "BizSuite Corp";
            ViewBag.Email = HttpContext.Session.GetString("Email") ?? "";
        }

        private int GetCompanyId()
        {
            var companyIdClaim = User.Claims.FirstOrDefault(c => c.Type == "CompanyId");
            return companyIdClaim != null ? int.Parse(companyIdClaim.Value) : 1;
        }
    }

    public class SearchResultViewModel
    {
        public string Source { get; set; } = string.Empty;
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
    }
}