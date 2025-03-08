﻿using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using reactBackend.Data;
using reactBackend.Models;
using reactBackend.Services;
using System.Text;
using DinkToPdf;
using DinkToPdf.Contracts;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// Add services to container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IOtpService, OtpService>();



// تسجيل خدمة الصور
builder.Services.AddScoped<IImageService, ImageService>();

// تسجيل خدمة DinkToPdf
builder.Services.AddSingleton(typeof(IConverter), new SynchronizedConverter(new PdfTools()));

// تسجيل خدمة معالجة الدفع
builder.Services.AddScoped<IPaymentService, PaymentService>();

// تكوين حجم الملف الأقصى المسموح به للتحميل
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 2 * 1024 * 1024; // 2MB
});

// تكوين خدمة الملفات الثابتة
builder.Services.AddDirectoryBrowser();



// Configure DbContext
var connectionString = builder.Environment.IsDevelopment()
   ? builder.Configuration.GetConnectionString("LocalConnection")
   : builder.Configuration.GetConnectionString("AzureConnection");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);
    });
});

// إضافة تسجيل لأخطاء الاتصال بقاعدة البيانات
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add Identity Services
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = false; // تم التعديل للتسهيل
    options.Password.RequireUppercase = false; // تم التعديل للتسهيل
    options.Password.RequiredLength = 6;
    options.Password.RequiredUniqueChars = 1;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", 
        builder =>
        {
            builder
                .WithOrigins(
                    "https://recat-onlinestore.netlify.app",
                    "http://localhost:5173"
                                    )
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
});

// Configure Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration.GetSection("JWT:Key").Value!)),
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidIssuer = builder.Configuration.GetSection("JWT:Issuer").Value,
        ValidAudience = builder.Configuration.GetSection("JWT:Audience").Value,
        ClockSkew = TimeSpan.Zero
    };
});

var app = builder.Build();

// Configure Swagger
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "React Online Store API v1");
    c.RoutePrefix = string.Empty;
});

// إضافة دعم الملفات الثابتة
app.UseStaticFiles();

// تكوين مجلد الصور
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
       Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "images")),
    RequestPath = "/images"
});

// إنشاء مجلد الفواتير إذا لم يكن موجودًا
string invoiceDirectory = Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "invoices");
if (!Directory.Exists(invoiceDirectory))
{
    Directory.CreateDirectory(invoiceDirectory);
}

// تكوين مجلد الفواتير
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(invoiceDirectory),
    RequestPath = "/invoices"
});

// Basic health check endpoint
app.MapGet("/health", () => Results.Ok("API is running!"));

// Global error handling
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = 500;
        await context.Response.WriteAsJsonAsync(new
        {
            error = "An error occurred",
            message = app.Environment.IsDevelopment() ? ex.Message : "Internal server error"
        });
    }
});

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("AllowReactApp");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();


// Ensure database is created and migrations are applied
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        await context.Database.MigrateAsync();

        // إضافة الأدوار الأساسية
        if (!await roleManager.RoleExistsAsync("admin"))
            await roleManager.CreateAsync(new IdentityRole("admin"));

        if (!await roleManager.RoleExistsAsync("user"))
            await roleManager.CreateAsync(new IdentityRole("user"));

        // إضافة حساب المدير التجريبي
        if (!await userManager.Users.AnyAsync(u => u.Email == "admin@example.com"))
        {
            var adminUser = new ApplicationUser
            {
                UserName = "admin",
                Email = "admin@example.com",
                Name = "المدير",
                PhoneNumber = "0553065029",
                EmailConfirmed = true,
                PhoneNumberConfirmed = true
            };

            var result = await userManager.CreateAsync(adminUser, "admin123");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "admin");
            }
        }

        // إضافة حساب المستخدم التجريبي
        if (!await userManager.Users.AnyAsync(u => u.Email == "user@example.com"))
        {
            var normalUser = new ApplicationUser
            {
                UserName = "user",
                Email = "user@example.com",
                Name = "مستخدم",
                PhoneNumber = "0553065028",
                EmailConfirmed = true,
                PhoneNumberConfirmed = true
            };

            var result = await userManager.CreateAsync(normalUser, "user123");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(normalUser, "user");
            }
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database.");
    }
}

app.Run();