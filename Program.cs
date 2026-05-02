using Newtonsoft.Json;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

namespace BizSuite
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // ── MVC ──────────────────────────────────────────────────────────
            builder.Services.AddControllersWithViews()
                .AddNewtonsoftJson(options =>
                {
                    options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
                });

            // ── Infrastructure ───────────────────────────────────────────────
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<BizSuite.Data.DbHelper>();

            // ── Repositories ─────────────────────────────────────────────────
            builder.Services.AddScoped<BizSuite.Repositories.IProductRepository,    BizSuite.Repositories.ProductRepository>();
            builder.Services.AddScoped<BizSuite.Repositories.IUserRepository,       BizSuite.Repositories.UserRepository>();
            builder.Services.AddScoped<BizSuite.Repositories.IStaffRepository,      BizSuite.Repositories.StaffRepository>();
            builder.Services.AddScoped<BizSuite.Repositories.IDashboardRepository,  BizSuite.Repositories.DashboardRepository>();
            builder.Services.AddScoped<BizSuite.Repositories.ICustomerRepository,   BizSuite.Repositories.CustomerRepository>();
            builder.Services.AddScoped<BizSuite.Repositories.ISalesRepository,      BizSuite.Repositories.SalesRepository>();
            builder.Services.AddScoped<BizSuite.Repositories.IPaymentRepository,    BizSuite.Repositories.PaymentRepository>();
            builder.Services.AddScoped<BizSuite.Repositories.IFulfillmentRepository,BizSuite.Repositories.FulfillmentRepository>();

            // ── Services ─────────────────────────────────────────────────────
            builder.Services.AddScoped<BizSuite.Services.IEmailService,   BizSuite.Services.EmailService>();
            builder.Services.AddScoped<BizSuite.Services.IShippingService, BizSuite.Services.ShippingService>();
            builder.Services.AddHostedService<BizSuite.Services.FulfillmentBackgroundService>();

            // ── Session (1-minute idle timeout for demo auto-logout) ──────────
            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout        = TimeSpan.FromMinutes(10);
                options.Cookie.HttpOnly    = true;
                options.Cookie.IsEssential = true;
                options.Cookie.Name        = ".BizSuite.Session";
                options.Cookie.SameSite    = SameSiteMode.Strict;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            });

            // ── Cookie Authentication (1-minute expiry for demo auto-logout) ──
            builder.Services.AddAuthentication(
                Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.LoginPath        = "/Auth/Login";
                    options.LogoutPath       = "/Auth/Logout";
                    options.AccessDeniedPath = "/Auth/AccessDenied";
                    options.ExpireTimeSpan   = TimeSpan.FromMinutes(10);   // ← 1 min for demo
                    options.SlidingExpiration = true;
                    options.Cookie.SameSite  = SameSiteMode.Strict;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                    options.Cookie.HttpOnly  = true;
                });

            // ── Rate Limiting (brute-force protection on auth endpoints) ─────
            builder.Services.AddRateLimiter(opts =>
            {
                opts.AddSlidingWindowLimiter("AuthPolicy", limiterOpts =>
                {
                    limiterOpts.PermitLimit         = 10;
                    limiterOpts.Window              = TimeSpan.FromMinutes(10);
                    limiterOpts.SegmentsPerWindow   = 5;
                    limiterOpts.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    limiterOpts.QueueLimit          = 0;
                });

                opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                opts.OnRejected = async (ctx, _) =>
                {
                    ctx.HttpContext.Response.ContentType = "text/plain";
                    await ctx.HttpContext.Response.WriteAsync(
                        "Too many requests. Please wait a moment before trying again.");
                };
            });

            var app = builder.Build();

            // ── Stripe ───────────────────────────────────────────────────────
            Stripe.StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];

            // ── Error handling ───────────────────────────────────────────────
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }
            else
            {
                app.UseDeveloperExceptionPage();
            }

            // ── Security Headers ─────────────────────────────────────────────
            app.Use(async (ctx, next) =>
            {
                ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
                ctx.Response.Headers["X-Frame-Options"]        = "DENY";
                ctx.Response.Headers["Referrer-Policy"]        = "strict-origin-when-cross-origin";
                ctx.Response.Headers["X-XSS-Protection"]      = "1; mode=block";
                ctx.Response.Headers["Permissions-Policy"]     = "camera=(), microphone=(), geolocation=()";
                await next();
            });

            // ── Middleware Pipeline (ORDER MATTERS) ───────────────────────────
            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseRateLimiter();

            // ✅ Session MUST come before Authentication so session data
            //    is available during auth cookie validation
            app.UseSession();
            app.UseAuthentication();
            app.UseAuthorization();

            // ── Routes ───────────────────────────────────────────────────────
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Auth}/{action=Login}/{id?}");

            // ── Startup configuration warnings ───────────────────────────────
            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            var config = app.Services.GetRequiredService<IConfiguration>();

            if (string.IsNullOrEmpty(config.GetConnectionString("BizSuiteDb")))
                logger.LogCritical("STARTUP: Connection string 'BizSuiteDb' is missing!");

            if (string.IsNullOrEmpty(config["SmtpSettings:Host"]))
                logger.LogWarning("STARTUP: SMTP host is not configured. Email features will be limited.");

            if (config["Stripe:SecretKey"]?.StartsWith("sk_test_placeholder") == true)
                logger.LogWarning("STARTUP: Stripe secret key is a placeholder. Payment features will not work.");

            app.Run();
        }
    }
}