================================================================================
  BIZSUITE ERP
  A Multi-Tenant Enterprise Resource Planning System
================================================================================

  Built with  : ASP.NET Core 8 MVC (.NET 8)
  Database    : Microsoft SQL Server 2022
  Auth        : Cookie-based session authentication
  Payments    : Stripe
  Shipping    : EasyPost
  Deployment  : Docker / IIS / Azure App Service

--------------------------------------------------------------------------------
  TABLE OF CONTENTS
--------------------------------------------------------------------------------

  1.  Project Overview
  2.  Features & Modules
  3.  Technology Stack
  4.  Project Structure
  5.  Database Setup
  6.  Configuration (appsettings)
  7.  Running Locally
  8.  Running with Docker
  9.  Deployment Notes
  10. Security Architecture
  11. Key Dependencies (NuGet Packages)
  12. Environment Variables Reference
  13. Roles & Permissions
  14. Developer Notes

--------------------------------------------------------------------------------
  1. PROJECT OVERVIEW
--------------------------------------------------------------------------------

BizSuite ERP is a full-featured, multi-tenant Enterprise Resource Planning
system designed for small and medium-sized businesses. Each registered company
(tenant) gets a fully isolated data environment — rows belonging to one company
are invisible to another, enforced at the database level using SQL Server
Row-Level Security (RLS).

The system covers the full business lifecycle:
  Customer acquisition (CRM + Leads) → Inventory → Sales Orders →
  Invoicing → Payments → Fulfilment → Reporting

--------------------------------------------------------------------------------
  2. FEATURES & MODULES
--------------------------------------------------------------------------------

  [AUTH]
  - User registration with company on-boarding (tenant creation)
  - Login / Logout with sliding-window cookie authentication
  - Forgot Password / Reset Password via email OTP
  - Profile page with photo upload (stored as binary in DB)
  - Session idle timeout (10 minutes)

  [ADMIN DASHBOARD]
  - KPI cards: Total Customers, Total Products, Total Sales,
    Sales This Month vs Last Month
  - Low-stock alerts panel
  - Top 5 Customers by lifetime spend
  - Top 5 Revenue Categories (chart-ready data)
  - Monthly sales trend for the last 12 months
  - Top 5 Best-Selling Products

  [CRM – CUSTOMERS]
  - Create / Edit / Soft-delete customers
  - Customer photo upload (stored as VARBINARY in DB, served via controller)
  - GST fields: GSTIN, State Code
  - Customer intelligence view: tier (Gold / Silver / Bronze),
    lifetime value, days since last purchase
  - Order count and total spend per customer

  [INVENTORY – PRODUCTS]
  - Create / Edit / Soft-delete products with categories and suppliers
  - Product image upload (binary storage, no file system dependency)
  - SKU, HSN Code, Barcode support
  - Stock quantity tracking with low-stock threshold alerts
  - Stock adjustment (IN / OUT / ADJUSTMENT) with full ledger history
  - Published / Unpublished toggle for storefront visibility

  [SALES ORDERS]
  - Multi-line order entry with product picker
  - Auto-deduction of stock on order creation
  - 18% GST applied automatically to order total
  - Order status lifecycle: Pending → Confirmed → Shipped → Delivered
  - View order detail with line items breakdown

  [INVOICES & PAYMENTS]
  - Invoice auto-generated on every sales order
  - Payment recording: Cash, Card, UPI, Bank Transfer
  - Partial payment support — status: Pending → Partial → Paid
  - Invoice print view

  [FULFILLMENT]
  - Fulfillment board with order queue
  - One-click status updates (Pick → Pack → Ship → Deliver)
  - Bulk status update support
  - Carrier & tracking number entry per order
  - Shipped date recording
  - Packing slip printable view
  - Side-drawer order preview (click-less UX)
  - Background service for auto-processing shipment webhooks

  [STAFF MANAGEMENT]
  - Create / Edit / Deactivate staff members (users)
  - Role assignment: Admin, Manager, Staff
  - Staff attendance: Clock In / Clock Out
  - Auto clock-out protection (background job)
  - Staff dashboard with daily attendance summary

  [PAYMENTS / SUBSCRIPTIONS]
  - Stripe integration for subscription billing
  - Subscription plans: Free, Basic, Premium
  - Stripe webhook listener for payment event processing
  - Checkout and payment success pages

  [AUDIT LOGS]
  - Every create / update / delete action is logged
  - Logs include: user, table, action type, timestamp, IP address

  [REPORTS]
  - Downloadable reports (CSV / Print)
  - Sales reports by date range

  [SEARCH]
  - Global search across customers, products, and orders

--------------------------------------------------------------------------------
  3. TECHNOLOGY STACK
--------------------------------------------------------------------------------

  Layer               Technology
  ──────────────────  ──────────────────────────────────────────────────────
  Web Framework       ASP.NET Core 8 MVC
  Language            C# 12
  Database            Microsoft SQL Server 2022 (T-SQL, Stored Procedures)
  ORM / Data Access   Raw ADO.NET via Microsoft.Data.SqlClient (no EF queries)
  Authentication      ASP.NET Core Cookie Authentication
  Password Hashing    BCrypt.Net-Next (v4.1.0)
  JSON Handling       Newtonsoft.Json (via Mvc.NewtonsoftJson)
  Payments            Stripe.net (v51.0.0)
  Shipping            EasyPost-Official (v7.7.1)
  Front-end CSS       Bootstrap 5 + Custom BizSuite CSS (dark theme)
  Front-end JS        Vanilla JS + jQuery + Bootstrap Bundle
  Form Validation     jQuery Validation + Unobtrusive Validation
  Rate Limiting       ASP.NET Core Built-in Rate Limiter (sliding window)
  Containerization    Docker (multi-stage build, .dockerignore included)
  Dev IDE             Visual Studio 2022 / VS Code

--------------------------------------------------------------------------------
  4. PROJECT STRUCTURE
--------------------------------------------------------------------------------

  BizSuite/
  ├── Controllers/
  │   ├── AdminController.cs          – Dashboard, Customers, Products, Reports
  │   ├── AuthController.cs           – Login, Register, Password Reset
  │   ├── FulfillmentController.cs    – Fulfillment board & order processing
  │   ├── HomeController.cs           – Landing / Error pages
  │   ├── PaymentController.cs        – Stripe checkout & webhooks
  │   ├── ProductController.cs        – Product image serving
  │   ├── StaffController.cs          – Staff list, attendance, dashboard
  │   └── WebhookController.cs        – EasyPost / Stripe webhook endpoints
  │
  ├── Data/
  │   └── DbHelper.cs                 – ADO.NET connection factory & helpers
  │
  ├── Database/
  │   ├── master_schema.sql           – Full DB schema (run first on fresh DB)
  │   └── master_logic.sql            – All SPs, views, indexes, RLS (run second)
  │
  ├── Models/
  │   ├── Auth/                       – Login, Register, ResetPassword ViewModels
  │   ├── Dashboard/                  – Admin & Staff dashboard ViewModels
  │   ├── Staff/                      – StaffMember, StaffViewModel
  │   ├── Customer.cs / CustomerViewModel.cs
  │   ├── Product.cs
  │   ├── SalesViewModel.cs
  │   ├── SettingsViewModel.cs
  │   ├── PaginationModels.cs
  │   └── WebhookLog.cs
  │
  ├── Repositories/
  │   ├── I*.cs                       – Repository interfaces
  │   ├── CustomerRepository.cs
  │   ├── DashboardRepository.cs
  │   ├── FulfillmentRepository.cs
  │   ├── PaymentRepository.cs
  │   ├── ProductRepository.cs
  │   ├── SalesRepository.cs
  │   ├── StaffRepository.cs
  │   └── UserRepository.cs
  │
  ├── Services/
  │   ├── EmailService.cs             – SMTP email sending (OTP, notifications)
  │   ├── IEmailService.cs
  │   ├── ShippingService.cs          – EasyPost API wrapper
  │   ├── IShippingService.cs
  │   └── FulfillmentBackgroundService.cs – Background hosted service
  │
  ├── Views/
  │   ├── Admin/                      – Dashboard, Customers, Products, etc.
  │   ├── Auth/                       – Login, Register, ForgotPassword, etc.
  │   ├── Fulfillment/                – Index, Details, PackingSlip
  │   ├── Home/                       – Index, Privacy
  │   ├── Payment/                    – Checkout, Success
  │   ├── Staff/                      – Dashboard, StaffList, OrderEntry, etc.
  │   └── Shared/                     – _AppLayout, _AuthLayout, _Sidebar, etc.
  │
  ├── wwwroot/
  │   ├── css/
  │   │   ├── bizsuite.css            – Custom dark/green theme
  │   │   └── site.css
  │   ├── js/
  │   │   ├── bizsuite.js             – Custom JS
  │   │   └── site.js
  │   ├── images/                     – Logo assets
  │   ├── assets/images/              – Default product placeholder
  │   └── lib/                        – Bootstrap 5, jQuery, jQuery Validation
  │
  ├── appsettings.json                – Base config (placeholders only)
  ├── appsettings.Development.json    – Dev overrides
  ├── appsettings.Production.json     – PRODUCTION SECRETS (git-ignored!)
  ├── Dockerfile                      – Container image definition
  ├── .dockerignore                   – Files excluded from Docker build
  ├── .gitignore                      – Files excluded from git
  ├── BizSuite.csproj                 – Project file (.NET 8)
  ├── BizSuite.slnx                   – Solution file
  ├── Program.cs                      – App entry point & DI configuration
  └── web.config                      – IIS configuration

--------------------------------------------------------------------------------
  5. DATABASE SETUP
--------------------------------------------------------------------------------

  STEP 1: Open SQL Server Management Studio (SSMS) or Azure Data Studio.

  STEP 2: Run the schema script to create the database and all tables:

          Database\master_schema.sql

          This creates:
            - Database: Bizsuite
            - Tables: SubscriptionPlans, Companies, Roles, Users, Attendance,
                      Customers, Leads, Categories, Suppliers, Products,
                      SalesOrders, OrderItems, Invoices, Payments,
                      StockTransactions, AuditLogs, Notifications, WebhookLogs
            - Seed data: 4 roles, 3 subscription plans, 1 default company

  STEP 3: Run the logic script to create all stored procedures, views, and policies:

          Database\master_logic.sql

          This creates:
            - User-Defined Table Type : OrderItemType
            - Schema                  : Security
            - Views                   : vw_SalesSummary, vw_CRM_CustomerIntelligence
            - Stored Procedures       : sp_GetUserAuthData, sp_RegisterTenant,
                                        sp_UpsertUser, sp_GetCustomers,
                                        sp_GetCustomerById, sp_UpsertCustomer,
                                        sp_GetProducts, sp_GetProductById,
                                        sp_UpsertProduct, sp_AdjustStock,
                                        sp_CreateSalesOrder, sp_GetInvoices,
                                        sp_AddPayment, sp_GetDashboardStats,
                                        sp_InsertAuditLog
            - Performance Indexes     : 9 targeted indexes (filtered + covering)
            - RLS Security Policies   : Tenant isolation on Customers, Products,
                                        SalesOrders, Invoices

  STEP 4: Create the first Admin user.
          Register via the app's /Auth/Register page — this automatically
          creates a new company (tenant) and assigns the Admin role.

--------------------------------------------------------------------------------
  6. CONFIGURATION (appsettings)
--------------------------------------------------------------------------------

  The app uses layered configuration. Edit the appropriate file:

  appsettings.json              – Shared base config (empty/placeholder values)
  appsettings.Development.json  – Local dev overrides
  appsettings.Production.json   – PRODUCTION ONLY (never commit to git!)

  Required configuration keys:

  ConnectionStrings:
    BizSuiteDb        SQL Server connection string for the Bizsuite database

  SmtpSettings:
    Host              SMTP server hostname  (e.g. smtp.gmail.com)
    Port              SMTP port             (usually 587)
    User              SMTP login username
    Pass              SMTP login password / app password
    From              Sender email address

  Stripe:
    PublishableKey    Stripe publishable key  (pk_live_... or pk_test_...)
    SecretKey         Stripe secret key       (sk_live_... or sk_test_...)
    WebhookSecret     Stripe webhook signing secret (whsec_...)

  EasyPost:
    ApiKey            EasyPost API key for shipping label generation

  IMPORTANT:
  - Never hard-code secrets in appsettings.json
  - Use dotnet user-secrets for local development:
      dotnet user-secrets set "ConnectionStrings:BizSuiteDb" "your_conn_string"
  - Use environment variables or Azure Key Vault for production

--------------------------------------------------------------------------------
  7. RUNNING LOCALLY
--------------------------------------------------------------------------------

  Prerequisites:
    - .NET 8 SDK          (https://dotnet.microsoft.com/download)
    - SQL Server 2019+    (Developer or Express edition is fine)
    - Visual Studio 2022+ or VS Code with C# extension

  Steps:

  1. Clone the repository:
       git clone https://github.com/YOUR-USERNAME/BizSuite.git
       cd BizSuite

  2. Set up the database (see Section 5 above).

  3. Update your connection string:
       Either edit appsettings.Development.json
       Or use dotnet user-secrets (recommended):
         dotnet user-secrets set "ConnectionStrings:BizSuiteDb" "Server=.;Database=Bizsuite;..."

  4. Restore NuGet packages:
       dotnet restore

  5. Run the application:
       dotnet run
       -- OR --
       Press F5 in Visual Studio

  6. Open in browser:
       https://localhost:5001   (HTTPS)
       http://localhost:5000    (HTTP)

  7. Register a new account at /Auth/Register to create your first company.

--------------------------------------------------------------------------------
  8. RUNNING WITH DOCKER
--------------------------------------------------------------------------------

  Build the Docker image:

    docker build -t bizsuite-erp .

  Run the container (pass secrets as environment variables):

    docker run -d -p 8080:80 \
      -e "ConnectionStrings__BizSuiteDb=Server=host.docker.internal;..." \
      -e "Stripe__SecretKey=sk_live_..." \
      -e "SmtpSettings__Host=smtp.gmail.com" \
      -e "SmtpSettings__Port=587" \
      -e "SmtpSettings__User=you@example.com" \
      -e "SmtpSettings__Pass=your_app_password" \
      -e "SmtpSettings__From=noreply@yourdomain.com" \
      -e "ASPNETCORE_ENVIRONMENT=Production" \
      --name bizsuite \
      bizsuite-erp

  The app will be available at: http://localhost:8080

  Note: The SQL Server instance must be accessible from within the container.
        Use "host.docker.internal" on Windows/Mac to refer to the host machine.

--------------------------------------------------------------------------------
  9. DEPLOYMENT NOTES
--------------------------------------------------------------------------------

  IIS Deployment:
    1. Publish the project: dotnet publish -c Release -o ./publish
    2. Install the ASP.NET Core Hosting Bundle on the server
    3. Create a new IIS site pointing to the publish folder
    4. Set environment variable ASPNETCORE_ENVIRONMENT=Production in IIS
    5. Place production secrets in appsettings.Production.json on the server
       (never commit this file to git)

  Azure App Service:
    1. Push to GitHub and connect via Azure Deployment Center
    2. Set all secrets as App Service Application Settings (environment vars)
    3. Connection strings go under the "Connection strings" section in Azure

  Always ensure:
    - HTTPS is enforced (SSL certificate installed)
    - appsettings.Production.json is on the server but NOT in the repository
    - SQL Server firewall allows connections from the app server IP

--------------------------------------------------------------------------------
  10. SECURITY ARCHITECTURE
--------------------------------------------------------------------------------

  Multi-Tenant Row-Level Security (RLS):
    - SQL Server Security schema with fn_TenantIsolationPredicate
    - SESSION_CONTEXT(N'CompanyId') is set on every DB connection
    - Filter + Block predicates applied to Customers, Products,
      SalesOrders, and Invoices tables
    - System admins bypass RLS via IsSystemAdmin session flag

  Application Security:
    - All passwords hashed with BCrypt (work factor 12+)
    - HttpOnly, Strict SameSite, Secure cookies
    - Sliding-window rate limiter on auth endpoints (10 req / 10 min)
    - Security response headers: X-Frame-Options DENY, X-XSS-Protection,
      X-Content-Type-Options nosniff, Referrer-Policy, Permissions-Policy
    - HSTS enforced in production
    - Anti-forgery tokens on all state-changing forms
    - Role-based authorization on all controllers ([Authorize(Roles="Admin")])

  What is NEVER committed to git:
    - appsettings.Production.json  (connection strings, API keys)
    - bin/, obj/                   (build artifacts)
    - .vs/, *.user                 (IDE files)

--------------------------------------------------------------------------------
  11. KEY DEPENDENCIES (NuGet PACKAGES)
--------------------------------------------------------------------------------

  Package                           Version   Purpose
  ────────────────────────────────  ────────  ──────────────────────────────
  BCrypt.Net-Next                   4.1.0     Password hashing
  EasyPost-Official                 7.7.1     Shipping label & tracking API
  Microsoft.AspNetCore.Mvc          8.0.0     MVC framework (included in SDK)
  Microsoft.AspNetCore.Mvc.         8.0.0     Newtonsoft JSON serializer
    NewtonsoftJson
  Microsoft.Data.SqlClient          5.2.0     SQL Server ADO.NET driver
  Microsoft.EntityFrameworkCore     9.0.4     Used for tooling/migrations only
  Stripe.net                        51.0.0    Stripe payments SDK

--------------------------------------------------------------------------------
  12. ENVIRONMENT VARIABLES REFERENCE
--------------------------------------------------------------------------------

  When deploying via Docker or cloud platforms, use double underscores (__)
  to represent nested JSON keys:

  Variable Name                           Maps To (appsettings.json)
  ──────────────────────────────────────  ────────────────────────────────────
  ConnectionStrings__BizSuiteDb           ConnectionStrings.BizSuiteDb
  SmtpSettings__Host                      SmtpSettings.Host
  SmtpSettings__Port                      SmtpSettings.Port
  SmtpSettings__User                      SmtpSettings.User
  SmtpSettings__Pass                      SmtpSettings.Pass
  SmtpSettings__From                      SmtpSettings.From
  Stripe__PublishableKey                  Stripe.PublishableKey
  Stripe__SecretKey                       Stripe.SecretKey
  Stripe__WebhookSecret                   Stripe.WebhookSecret
  EasyPost__ApiKey                        EasyPost.ApiKey
  ASPNETCORE_ENVIRONMENT                  Environment name (Development /
                                          Production)

--------------------------------------------------------------------------------
  13. ROLES & PERMISSIONS
--------------------------------------------------------------------------------

  Role        Access Level
  ──────────  ──────────────────────────────────────────────────────────────
  Admin       Full access: Dashboard, CRM, Inventory, Orders, Invoices,
              Payments, Fulfillment, Staff Management, Settings, Reports,
              Audit Logs
  Manager     Same as Admin (configurable per business requirement)
  Staff       Staff Dashboard, Order Entry, Inventory Check, Attendance
  Developer   Reserved for system-level access (future use)

  Default Login Page: /Auth/Login
  Default Admin Route after login: /Admin/Dashboard
  Default Staff Route after login: /Staff/Dashboard

--------------------------------------------------------------------------------
  14. DEVELOPER NOTES
--------------------------------------------------------------------------------

  Design Patterns:
    - Repository Pattern    : All DB access goes through I*Repository interfaces
    - Dependency Injection  : All repos and services registered in Program.cs
    - Stored Procedures Only: Zero raw inline SQL in application code
                              All queries are executed via named SPs
    - Soft Deletes          : Records are never hard-deleted; IsDeleted = 1

  Image Storage Strategy:
    - Product and Customer images stored as VARBINARY(MAX) in SQL Server
    - Served via controller action (e.g. /Product/Image/{id})
    - No file-system dependency — fully portable across servers/containers
    - Fallback to generated SVG placeholder if no image exists

  Session Management:
    - CompanyId, UserId, UserRole, FullName stored in Session
    - SESSION_CONTEXT set per DB connection for RLS enforcement
    - Idle timeout: 10 minutes (sliding expiration)

  Background Services:
    - FulfillmentBackgroundService runs as IHostedService
    - Polls for new webhook events and processes shipment status updates
    - Auto clock-out for staff who forgot to clock out

  Database Conventions:
    - All tables have CompanyId for tenant isolation
    - CreatedDate / ModifiedDate on all major entities
    - CreatedBy / ModifiedBy (UserId FK) on all major entities
    - All money columns: DECIMAL(18, 2)
    - All status columns: NVARCHAR(50)

  Adding a New Module Checklist:
    1. Create the table in master_schema.sql (and run on DB)
    2. Write stored procedures in master_logic.sql (CREATE OR ALTER)
    3. Add the Model class in Models/
    4. Add the IRepository interface in Repositories/
    5. Implement the Repository class in Repositories/
    6. Register the DI binding in Program.cs
    7. Create the Controller in Controllers/
    8. Create Views in Views/<ModuleName>/
    9. Add nav link in Views/Shared/_Sidebar.cshtml

================================================================================
  END OF README
================================================================================
