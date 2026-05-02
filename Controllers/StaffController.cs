using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using BizSuite.Models.Dashboard;
using BizSuite.Models.Staff;
using BizSuite.Repositories;
using System.Linq;
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using BizSuite.Models;
using BizSuite.Data;

namespace BizSuite.Controllers
{
    [Authorize(Roles = "Admin,Manager,Staff")]
    public class StaffController : Controller
    {
        private readonly IDashboardRepository _dashboardRepo;
        private readonly IStaffRepository _staffRepo;
        private readonly IProductRepository _productRepo;
        private readonly ISalesRepository _salesRepo;

        private readonly ICustomerRepository _customerRepo;

        private readonly ILogger<StaffController> _logger;
        private readonly DbHelper _db;

        public StaffController(
            IDashboardRepository dashboardRepo,
            IStaffRepository staffRepo,
            IProductRepository productRepo,
            ISalesRepository salesRepo,
            ICustomerRepository customerRepo, 
            ILogger<StaffController> logger,
            DbHelper db)
        {
            _dashboardRepo = dashboardRepo;
            _staffRepo = staffRepo;
            _productRepo = productRepo;
            _salesRepo = salesRepo;
            _customerRepo = customerRepo;
            _logger = logger;
            _db = db;
        }

        public IActionResult Dashboard()
        {
            SetViewBag();
            try
            {
                var vm = _dashboardRepo.GetStaffStats(
                    GetCompanyId(),
                    HttpContext.Session.GetString("FullName") ?? "Staff");
                
                // Fetch attendance state
                int userId = HttpContext.Session.GetInt32("UserId") ?? 1;
                int companyId = GetCompanyId();
                DateTime today = DateTime.Today;

                var dt = _db.ExecuteQuery(
                    "SELECT TOP 1 * FROM Attendance WHERE CompanyId = @C AND UserId = @U AND AttendanceDate = @D ORDER BY AttendanceId DESC",
                    DbHelper.P("@C", companyId),
                    DbHelper.P("@U", userId),
                    DbHelper.P("@D", today)
                );

                if (dt.Rows.Count > 0)
                {
                    var row = dt.Rows[0];
                    vm.IsClockedIn = row["ClockIn"] != DBNull.Value;
                    vm.IsClockedOut = row["ClockOut"] != DBNull.Value;
                    vm.ClockInTime = row["ClockIn"] != DBNull.Value ? Convert.ToDateTime(row["ClockIn"]) : null;
                    vm.ClockOutTime = row["ClockOut"] != DBNull.Value ? Convert.ToDateTime(row["ClockOut"]) : null;
                    vm.AttendanceId = Convert.ToInt32(row["AttendanceId"]);
                }

                return View(vm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading staff dashboard");
                TempData["Error"] = "Could not load dashboard.";
                return View(new StaffDashboardViewModel());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ClockIn()
        {
            try
            {
                int userId = HttpContext.Session.GetInt32("UserId") ?? 1;
                int companyId = GetCompanyId();
                DateTime today = DateTime.Today;
                DateTime now = DateTime.Now;

                // Check if already clocked in today
                var existingId = _db.ExecuteScalar("SELECT TOP 1 AttendanceId FROM Attendance WHERE CompanyId = @C AND UserId = @U AND AttendanceDate = @D",
                    DbHelper.P("@C", companyId), DbHelper.P("@U", userId), DbHelper.P("@D", today));

                if (existingId != null && existingId != DBNull.Value)
                {
                    return Json(new { success = false, message = "Already clocked in today." });
                }

                _db.ExecuteNonQuery("INSERT INTO Attendance (CompanyId, UserId, AttendanceDate, ClockIn) VALUES (@C, @U, @D, @ClockIn)",
                    DbHelper.P("@C", companyId), DbHelper.P("@U", userId), DbHelper.P("@D", today), DbHelper.P("@ClockIn", now));

                return Json(new { success = true, message = "Clocked in successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during clock in.");
                return Json(new { success = false, message = "System error during clock in." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ClockOut()
        {
            try
            {
                int userId = HttpContext.Session.GetInt32("UserId") ?? 1;
                int companyId = GetCompanyId();
                DateTime today = DateTime.Today;
                DateTime now = DateTime.Now;

                var dt = _db.ExecuteQuery("SELECT TOP 1 AttendanceId, ClockOut FROM Attendance WHERE CompanyId = @C AND UserId = @U AND AttendanceDate = @D ORDER BY AttendanceId DESC",
                    DbHelper.P("@C", companyId), DbHelper.P("@U", userId), DbHelper.P("@D", today));

                if (dt.Rows.Count == 0)
                {
                    return Json(new { success = false, message = "No clock in record found for today." });
                }

                var row = dt.Rows[0];
                if (row["ClockOut"] != DBNull.Value)
                {
                    return Json(new { success = false, message = "Already clocked out today." });
                }

                int attId = Convert.ToInt32(row["AttendanceId"]);
                _db.ExecuteNonQuery("UPDATE Attendance SET ClockOut = @ClockOut WHERE AttendanceId = @Id",
                    DbHelper.P("@ClockOut", now), DbHelper.P("@Id", attId));

                return Json(new { success = true, message = "Clocked out successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during clock out.");
                return Json(new { success = false, message = "System error during clock out." });
            }
        }

        public IActionResult OrderEntry()
        {
            SetViewBag();
            try
            {
                // ARCHITECT FIX: Fetch REAL customers from the database for the dropdown
                var customers = _customerRepo.GetAll(GetCompanyId());
                ViewBag.Customers = customers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load customers for POS");
                ViewBag.Customers = new List<Customer>();
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SubmitOrder([FromBody] OrderSubmitRequest request)
        {
            try
            {
                if (request == null || request.Items == null || !request.Items.Any())
                    return Json(new { success = false, message = "Cart is empty." });

                if (request.CustomerId <= 0)
                    return Json(new { success = false, message = "Please select a customer." });

                int companyId = GetCompanyId();
                int userId = HttpContext.Session.GetInt32("UserId") ?? 1;

                var (newOrderId, newInvoiceId) = _salesRepo.CreateSalesOrder(companyId, request.CustomerId, userId, request.Items);

                return Json(new
                {
                    success = true,
                    orderId = newOrderId,
                    invoiceId = newInvoiceId,
                    message = "Order placed successfully! Stock updated."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating sales order");
                
                return Json(new { success = false, message = "Transaction failed: " + ex.Message });
            }
        }

        public IActionResult InventoryCheck()
        {
            SetViewBag();
            try
            {
                var products = _productRepo.GetAll(GetCompanyId());
                return View(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading inventory");
                TempData["Error"] = "Could not load inventory.";
                return View(new List<Product>());
            }
        }

        [HttpGet]
        public IActionResult GetProducts()
        {
            try
            {
                
                var products = _productRepo.GetAll(GetCompanyId())
                    .Where(p => !p.IsDeleted)
                    .Select(p => new {
                        productId = p.ProductId,
                        productName = p.ProductName,
                        price = p.Price,
                        stockQuantity = p.StockQuantity
                    });

                return Json(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching products for POS");
                return Json(new { error = "Could not load products" });
            }
        }

        [Authorize(Roles = "Admin,Manager")]
        public IActionResult StaffList()
        {
            SetViewBag();
            var list = _staffRepo.GetAll(GetCompanyId());

            var userRole = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role)?.Value 
                          ?? HttpContext.Session.GetString("RoleName");
            
            if (userRole == "Manager")
            {
                list = list.Where(s => s.Role != "Admin").ToList();
            }
            
            return View(list);
        }

        [Authorize(Roles = "Admin,Manager")]
        public IActionResult CreateStaff()
        {
            SetViewBag();
            
            return View(new StaffViewModel { IsActive = true });
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Manager")]
        [ValidateAntiForgeryToken]
        public IActionResult CreateStaff(StaffViewModel model)
        {
            SetViewBag();
            if (!ModelState.IsValid) return View(model);

            try
            {
                
                string currentUserRole = HttpContext.Session.GetString("RoleName") ?? "Staff";
                if (currentUserRole == "Manager" && model.Role != "Staff")
                {
                    ModelState.AddModelError("Role", "As a Manager, you can only create Staff members, not Managers or Admins.");
                    return View(model);
                }

                var staff = new StaffMember
                {
                    CompanyId = GetCompanyId(),
                    FullName = model.FullName,
                    Email = model.Email,
                    Phone = model.Phone,
                    Role = model.Role,
                    Department = model.Department,
                    IsActive = model.IsActive
                };

                _staffRepo.Insert(staff);
                TempData["Success"] = $"Team member '{staff.FullName}' added successfully.";
                return RedirectToAction("StaffList");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating staff member");
                ModelState.AddModelError("", "Database error: " + ex.Message);
                return View(model);
            }
        }

        [Authorize(Roles = "Admin,Manager")]
        public IActionResult EditStaff(int id)
        {
            SetViewBag();
            var s = _staffRepo.GetById(id, GetCompanyId());
            if (s == null) return NotFound();

            var model = new StaffViewModel
            {
                StaffId = s.StaffId,
                FullName = s.FullName,
                Email = s.Email,
                Phone = s.Phone,
                Role = s.Role,
                Department = s.Department,
                IsActive = s.IsActive
            };
            return View(model);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Manager")]
        [ValidateAntiForgeryToken]
        public IActionResult EditStaff(StaffViewModel model)
        {
            SetViewBag();
            if (!ModelState.IsValid) return View(model);

            try
            {
                
                string currentUserRole = HttpContext.Session.GetString("RoleName") ?? "Staff";
                if (currentUserRole == "Manager" && model.Role != "Staff")
                {
                    
                    var existing = _staffRepo.GetById(model.StaffId, GetCompanyId());
                    if (existing != null && existing.Role != "Staff")
                    {
                        ModelState.AddModelError("", "You do not have permission to modify this role.");
                        return View(model);
                    }
                    model.Role = "Staff"; 
                }

                var staff = new StaffMember
                {
                    StaffId = model.StaffId,
                    CompanyId = GetCompanyId(),
                    FullName = model.FullName,
                    Email = model.Email,
                    Phone = model.Phone,
                    Role = model.Role,
                    Department = model.Department,
                    IsActive = model.IsActive
                };

                _staffRepo.Update(staff);
                TempData["Success"] = "Details updated successfully.";
                return RedirectToAction("StaffList");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating staff member");
                ModelState.AddModelError("", "Database error: " + ex.Message);
                return View(model);
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin")] 
        [ValidateAntiForgeryToken]
        public IActionResult DeleteStaff(int staffId)
        {
            try
            {
                _staffRepo.Delete(staffId, GetCompanyId());
                TempData["Success"] = "Member removed from team.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting staff {Id}", staffId);
                TempData["Error"] = "Failed to remove member.";
            }
            return RedirectToAction("StaffList");
        }

        private void SetViewBag()
        {
            ViewBag.FullName = HttpContext.Session.GetString("FullName") ?? "Staff";
            ViewBag.Email = HttpContext.Session.GetString("Email") ?? "";
            ViewBag.Role = HttpContext.Session.GetString("RoleName") ?? "Staff";
        }

        private int GetCompanyId()
        {
            var claim = User.Claims.FirstOrDefault(c => c.Type == "CompanyId");
            return claim != null ? int.Parse(claim.Value) : (HttpContext.Session.GetInt32("CompanyId") ?? 1);
        }
    }

    public class OrderSubmitRequest
    {
        public int CustomerId { get; set; }
        public List<OrderItemDto> Items { get; set; } = new List<OrderItemDto>();
    }
}