using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using BizSuite.Models.Auth;
using BizSuite.Repositories;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System;

namespace BizSuite.Controllers
{
    public class AuthController : Controller
    {
        private readonly IUserRepository _userRepo;
        private readonly ILogger<AuthController> _logger;
        private readonly Services.IEmailService _emailService;

        public AuthController(IUserRepository userRepo, ILogger<AuthController> logger, Services.IEmailService emailService)
        {
            _userRepo = userRepo;
            _logger = logger;
            _emailService = emailService;
        }


        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
                return RedirectToDashboard();

            ViewBag.ReturnUrl = returnUrl;
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("AuthPolicy")]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                var result = _userRepo.ValidateLogin(model.Email, model.Password);

                if (result == null)
                {
                    ModelState.AddModelError("", "Invalid email or password.");
                    return View(model);
                }

                HttpContext.Session.SetInt32("UserId", result.UserId);
                HttpContext.Session.SetInt32("CompanyId", result.CompanyId);
                HttpContext.Session.SetString("RoleName", result.RoleName);
                HttpContext.Session.SetString("FullName", result.FullName);
                HttpContext.Session.SetString("Email", result.Email);
                HttpContext.Session.SetString("Company", result.Company);
                HttpContext.Session.SetString("Phone", result.Phone ?? "");
                HttpContext.Session.SetString("Department", result.Department ?? "");
                HttpContext.Session.SetString("UserIdStr", result.UserId.ToString());

                if (result.ProfileImageData != null && result.ProfileImageData.Length > 0)
                {
                    string base64Image = Convert.ToBase64String(result.ProfileImageData);
                    string mimeType = string.IsNullOrEmpty(result.ProfileImageMimeType) ? "image/png" : result.ProfileImageMimeType;

                    string imageSrc = $"data:{mimeType};base64,{base64Image}";
                    HttpContext.Session.SetString("ProfileImage", imageSrc);
                }
                else
                {

                    HttpContext.Session.Remove("ProfileImage");
                }

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, result.UserId.ToString()),
                    new Claim(ClaimTypes.Name, result.FullName),
                    new Claim(ClaimTypes.Email, result.Email),
                    new Claim(ClaimTypes.Role, result.RoleName),
                    new Claim("CompanyId", result.CompanyId.ToString()),
                    new Claim("CompanyName", result.Company)
                };

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

                if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                    return Redirect(model.ReturnUrl);

                return RedirectToDashboard();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login error for {Email}", model.Email);
                ModelState.AddModelError("", "An error occurred. Please try again.");
                return View(model);
            }
        }

        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
                return RedirectToDashboard();

            ViewBag.ShowRegister = true;
            return View("Login", new RegisterViewModel());
        }

        [HttpGet]
        public IActionResult Terms()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Privacy()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.ShowRegister = true;
                return View("Login", model);
            }

            try
            {
                var result = _userRepo.RegisterTenant(model.CompanyName, model.FullName, model.Email, model.Password);

                if (result == null)
                {
                    ModelState.AddModelError("", "Registration failed. Email or Company might already exist.");
                    ViewBag.ShowRegister = true;
                    return View("Login", model);
                }

                HttpContext.Session.SetInt32("UserId", result.UserId);
                HttpContext.Session.SetInt32("CompanyId", result.CompanyId);
                HttpContext.Session.SetString("RoleName", result.RoleName);
                HttpContext.Session.SetString("FullName", result.FullName);
                HttpContext.Session.SetString("Email", result.Email);
                HttpContext.Session.SetString("Company", result.Company);
                HttpContext.Session.SetString("UserIdStr", result.UserId.ToString());

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, result.UserId.ToString()),
                    new Claim(ClaimTypes.Name, result.FullName),
                    new Claim(ClaimTypes.Email, result.Email),
                    new Claim(ClaimTypes.Role, result.RoleName),
                    new Claim("CompanyId", result.CompanyId.ToString()),
                    new Claim("CompanyName", result.Company)
                };

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

                TempData["Welcome"] = $"Welcome to BizSuite, {result.FullName}! Your workspace is ready.";
                return RedirectToAction("Dashboard", "Admin");

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Register error for {Email}", model.Email);
                ModelState.AddModelError("", ex.Message);
                ViewBag.ShowRegister = true;
                return View("Login", model);
            }
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        [Authorize]
        [HttpGet]
        public IActionResult Profile()
        {

            if (string.IsNullOrEmpty(HttpContext.Session.GetString("Phone")))
            {
                var email = HttpContext.Session.GetString("Email") ?? User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
                if (!string.IsNullOrEmpty(email))
                {
                    var result = _userRepo.GetProfile(email);
                    if (result != null)
                    {
                        HttpContext.Session.SetString("Phone", result.Phone ?? "");
                        HttpContext.Session.SetString("Department", result.Department ?? "");

                        if (result.ProfileImageData != null && result.ProfileImageData.Length > 0)
                        {
                            string base64 = Convert.ToBase64String(result.ProfileImageData);
                            HttpContext.Session.SetString("ProfileImage", $"data:{result.ProfileImageMimeType};base64,{base64}");
                        }
                    }
                }
            }
            return View();
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ChangePassword(string oldPassword, string newPassword, string confirmPassword)
        {
            if (newPassword != confirmPassword)
            {
                TempData["Error"] = "New passwords do not match.";
                return RedirectToAction("Profile");
            }

            int userId = HttpContext.Session.GetInt32("UserId") ??
                        int.Parse(User.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value);

            bool success = _userRepo.ChangePassword(userId, oldPassword, newPassword);

            if (success)
            {
                TempData["Success"] = "Password updated successfully!";
            }
            else
            {
                TempData["Error"] = "Invalid current password.";
            }

            return RedirectToAction("Profile");
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(string fullName, string email, string phone, string department, IFormFile? profileImage)
        {
            try
            {
                int userId = HttpContext.Session.GetInt32("UserId") ??
                            int.Parse(User.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value);

                byte[]? imageData = null;
                string? mimeType = null;

                if (profileImage != null && profileImage.Length > 0)
                {
                    using (var ms = new MemoryStream())
                    {
                        await profileImage.CopyToAsync(ms);
                        imageData = ms.ToArray();
                        mimeType = profileImage.ContentType;
                    }
                }

                _userRepo.UpdateProfile(userId, fullName, email, phone, department, imageData, mimeType);

                HttpContext.Session.SetString("FullName", fullName);
                HttpContext.Session.SetString("Email", email);
                HttpContext.Session.SetString("Phone", phone ?? "");
                HttpContext.Session.SetString("Department", department ?? "");

                if (imageData != null)
                {
                    string base64 = Convert.ToBase64String(imageData);
                    HttpContext.Session.SetString("ProfileImage", $"data:{mimeType};base64,{base64}");
                }

                TempData["Success"] = "Profile updated successfully!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile");
                TempData["Error"] = "An error occurred while updating your profile.";
            }

            return RedirectToAction("Profile");
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("AuthPolicy")]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                ViewBag.Error = "Please enter your email address.";
                return View();
            }

            var user = _userRepo.GetProfile(email);
            if (user == null)
            {
                // To prevent email enumeration, we still say we sent a code if the system is configured that way,
                // but for this ERP, we'll be direct.
                ViewBag.Error = "No account found with this email.";
                return View();
            }

            // Generate 6-digit code
            string code = new Random().Next(100000, 999999).ToString();

            bool saved = _userRepo.SaveResetCode(email, code);
            if (saved)
            {
                string subject = "BizSuite Password Reset Code";
                string message = $@"
                    <div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #e1e1e1; border-radius: 10px;'>
                        <h2 style='color: #0f3d2f;'>Password Reset Request</h2>
                        <p>You requested to reset your password for your BizSuite account.</p>
                        <p>Your verification code is:</p>
                        <div style='font-size: 24px; font-weight: bold; color: #cbfb45; background: #0f3d2f; padding: 10px 20px; border-radius: 5px; display: inline-block;'>
                            {code}
                        </div>
                        <p style='margin-top: 20px; color: #666;'>This code will expire in 15 minutes.</p>
                        <p>If you did not request this, please ignore this email.</p>
                    </div>";

                await _emailService.SendEmailAsync(email, subject, message);

                TempData["Email"] = email;
                return RedirectToAction("VerifyCode");
            }
            else
            {
                ViewBag.Error = "Failed to generate reset code. Please try again.";
                return View();
            }
        }

        [HttpGet]
        public IActionResult VerifyCode()
        {
            string? email = TempData["Email"]?.ToString();
            if (string.IsNullOrEmpty(email)) return RedirectToAction("ForgotPassword");

            TempData.Keep("Email");
            return View(new ForgotPasswordViewModel { Email = email });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult VerifyCode(string email, string code)
        {
            if (string.IsNullOrEmpty(code))
            {
                ViewBag.Error = "Please enter the verification code.";
                return View(new ForgotPasswordViewModel { Email = email });
            }

            bool isValid = _userRepo.VerifyResetCode(email, code);
            if (isValid)
            {
                TempData["Email"] = email;
                TempData["Verified"] = true;
                return RedirectToAction("ResetPassword");
            }
            else
            {
                ViewBag.Error = "Invalid or expired code.";
                return View(new ForgotPasswordViewModel { Email = email });
            }
        }

        [HttpGet]
        public IActionResult ResetPassword()
        {
            string? email = TempData["Email"]?.ToString();
            bool verified = TempData["Verified"] != null && (bool)TempData["Verified"];

            if (string.IsNullOrEmpty(email) || !verified)
                return RedirectToAction("ForgotPassword");

            TempData.Keep("Email");
            TempData.Keep("Verified");
            return View(new ForgotPasswordViewModel { Email = email });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ResetPassword(string email, string password, string confirmPassword)
        {
            if (password != confirmPassword)
            {
                ViewBag.Error = "Passwords do not match.";
                return View(new ForgotPasswordViewModel { Email = email });
            }

            string hash = BCrypt.Net.BCrypt.HashPassword(password);
            _userRepo.ResetPassword(email, hash);

            TempData["Success"] = "Your password has been reset successfully. Please login with your new password.";
            return RedirectToAction("Login");
        }

        private IActionResult RedirectToDashboard()
        {

            var role = HttpContext.Session.GetString("RoleName")
                       ?? User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;

            return role == "Admin"
                ? RedirectToAction("Dashboard", "Admin")
                : RedirectToAction("Dashboard", "Staff");
        }
    }
}