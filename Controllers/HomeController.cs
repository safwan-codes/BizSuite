using BizSuite.Models;
using BizSuite.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Diagnostics;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace BizSuite.Controllers
{
    [AllowAnonymous] 
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly BizSuite.Repositories.IUserRepository _userRepository;

        public HomeController(ILogger<HomeController> logger, BizSuite.Repositories.IUserRepository userRepository)
        {
            _logger = logger;
            _userRepository = userRepository;
        }

        [HttpGet]
        public IActionResult Index()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToDashboard(User.FindFirstValue(ClaimTypes.Role));
            }
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToDashboard(User.FindFirstValue(ClaimTypes.Role));
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("AuthPolicy")]
        public async Task<IActionResult> Login(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.ErrorMessage = "Email and Password are required.";
                return View();
            }

            try
            {
                var user = _userRepository.ValidateLogin(email.Trim(), password);

                if (user != null)
                {
                    await IssueSecureCookieAsync(
                        user.UserId.ToString(),
                        user.FullName,
                        user.Email,
                        user.RoleName,
                        user.CompanyId.ToString(),
                        user.Company,
                        user.ProfileImageData,
                        user.ProfileImageMimeType
                    );

                    return RedirectToDashboard(user.RoleName);
                }

                ViewBag.ErrorMessage = "Invalid Email or Password.";
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SaaS Auth Error: Login failure for {Email}", email);
                ViewBag.ErrorMessage = "A secure connection could not be established.";
                return View();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("AuthPolicy")]
        public async Task<IActionResult> Register(string companyName, string fullName, string email, string password)
        {
            if (string.IsNullOrWhiteSpace(companyName) || string.IsNullOrWhiteSpace(fullName) ||
                string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.ErrorMessage = "All fields are required to create a workspace.";
                ViewBag.ShowRegister = true;
                return View("Login");
            }

            if (password.Length < 8)
            {
                ViewBag.ErrorMessage = "Password must be at least 8 characters for workspace security.";
                ViewBag.ShowRegister = true;
                return View("Login");
            }

            try
            {
                var user = _userRepository.RegisterTenant(companyName.Trim(), fullName.Trim(), email.Trim(), password);

                if (user != null)
                {
                    await IssueSecureCookieAsync(
                        user.UserId.ToString(),
                        user.FullName,
                        user.Email,
                        user.RoleName,
                        user.CompanyId.ToString(),
                        user.Company,
                        null,
                        null
                    );

                    return RedirectToDashboard(user.RoleName);
                }

                ViewBag.ErrorMessage = "Registration failed. Please contact support.";
                ViewBag.ShowRegister = true;
                return View("Login");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SaaS Auth Error: Registration failure for {Email}", email);
                ViewBag.ErrorMessage = "Tenant provisioning failed: " + ex.Message;
                ViewBag.ShowRegister = true;
                return View("Login");
            }
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Home");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        [HttpGet]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        #region Private SaaS Infrastructure Helpers

        private async Task IssueSecureCookieAsync(string userId, string fullName, string email, string role, string companyId, string companyName, byte[]? profileImageData, string? profileImageMimeType)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, fullName),
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Role, role),
                new Claim("CompanyId", companyId),
                new Claim("CompanyName", companyName)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent   = true,
                ExpiresUtc     = DateTimeOffset.UtcNow.AddMinutes(1),  // 1 min for demo auto-logout
                AllowRefresh   = true
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            HttpContext.Session.SetString("FullName", fullName);
            HttpContext.Session.SetString("RoleName", role);
            HttpContext.Session.SetString("CompanyId", companyId);

            if (profileImageData != null && profileImageData.Length > 0 && !string.IsNullOrEmpty(profileImageMimeType))
            {
                string base64 = Convert.ToBase64String(profileImageData);
                string imgSrc = $"data:{profileImageMimeType};base64,{base64}";
                HttpContext.Session.SetString("ProfileImage", imgSrc);
            }
        }

        private IActionResult RedirectToDashboard(string? role)
        {
            if (role == "Admin" || role == "PlatformAdmin")
            {
                return RedirectToAction("Dashboard", "Admin");
            }

            return RedirectToAction("Dashboard", "Staff");
        }

        #endregion
    }
}
