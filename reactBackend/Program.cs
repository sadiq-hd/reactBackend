using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using reactBackend.Data;
using reactBackend.Models;
using reactBackend.Services;
using System.Text;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// إضافة تسجيل متقدم
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.Services.Configure<LoggerFilterOptions>(options =>
{
    options.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Information);
    options.AddFilter("Microsoft.EntityFrameworkCore.Infrastructure", LogLevel.Information);
});

var logger = LoggerFactory.Create(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
    logging.SetMinimumLevel(LogLevel.Debug);
}).CreateLogger("Program");

logger.LogInformation("بدء تهيئة التطبيق");

try
{
    // Add services to container
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    builder.Services.AddSingleton<IOtpService, OtpService>();

    // تسجيل خدمة الصور
    builder.Services.AddScoped<IImageService, ImageService>();

    // تسجيل خدمة PDF الجديدة
    logger.LogInformation("تسجيل خدمة SelectPdf للتوافق مع Azure");
    builder.Services.AddSingleton<IPdfService, SelectPdfService>();

    // تسجيل DummyPdfConverter كخدمة احتياطية للتوافق مع الكود القديم
    builder.Services.AddSingleton<DummyPdfConverter>();

    // تسجيل خدمة معالجة الدفع
    builder.Services.AddScoped<IPaymentService, PaymentService>();

    // تكوين حجم الملف الأقصى المسموح به للتحميل
    builder.Services.Configure<FormOptions>(options =>
    {
        options.MultipartBodyLengthLimit = 2 * 1024 * 1024; // 2MB
    });

    // تكوين خدمة الملفات الثابتة
    builder.Services.AddDirectoryBrowser();

    builder.Services.AddScoped<IPurchaseVerificationService, PurchaseVerificationService>();

    // الحصول على سلسلة الاتصال
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (connectionString != null)
    {
        logger.LogInformation($"سلسلة الاتصال: {connectionString.Substring(0, Math.Min(30, connectionString.Length))}...");
    }
    else
    {
        logger.LogWarning("سلسلة الاتصال غير محددة!");
    }

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
    {
        if (connectionString != null)
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);

                sqlOptions.CommandTimeout(30);
            });
        }
        else
        {
            logger.LogError("لا يمكن تكوين سياق قاعدة البيانات بدون سلسلة اتصال صالحة");
        }
    });

    // Add Identity Services
    builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequiredLength = 6;
        options.Password.RequiredUniqueChars = 1;

        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;

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
                        "https://shuttercart.netlify.app",
                        "http://localhost:5173",
                      "http://localhost:5000",
                    "http://localhost:3000",
                    "https://localhost:5000",
                  "https://reactonlinestore-eebshvegccajfmfh.eastasia-01.azurewebsites.net",
                        "https://reactbackend-production.up.railway.app"
                    )
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
    });

    // ---- FIX: إصلاح JWT ----
    // التحقق من وجود إعدادات JWT وتوفير قيم افتراضية إذا كانت مفقودة
    var jwtKey = builder.Configuration.GetSection("JWT:Key").Value;
    var jwtIssuer = builder.Configuration.GetSection("JWT:Issuer").Value;
    var jwtAudience = builder.Configuration.GetSection("JWT:Audience").Value;

    // توفير قيم افتراضية إذا كانت الإعدادات مفقودة
    if (string.IsNullOrEmpty(jwtKey))
    {
        jwtKey = "mK8yP$9qL#nX2vR5tA7wE4hJ@cF3bN6dQ9wB8mH2pY5xK4jM7nF1vC6tZ3"; // مفتاح افتراضي قوي
        logger.LogWarning("JWT:Key غير موجود في الإعدادات. تم استخدام قيمة افتراضية.");
    }

    if (string.IsNullOrEmpty(jwtIssuer))
    {
        jwtIssuer = "https://reactonlinestore-eebshvegccajfmfh.eastasia-01.azurewebsites.net";
        logger.LogWarning("JWT:Issuer غير موجود في الإعدادات. تم استخدام قيمة افتراضية.");
    }

    if (string.IsNullOrEmpty(jwtAudience))
    {
        jwtAudience = "https://reactonlinestore-eebshvegccajfmfh.eastasia-01.azurewebsites.net";
        logger.LogWarning("JWT:Audience غير موجود في الإعدادات. تم استخدام قيمة افتراضية.");
    }

    // Configure Authentication مع معالجة القيم المفقودة
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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            ClockSkew = TimeSpan.Zero
        };
    });

    var app = builder.Build();

    // إضافة نقطة نهاية اختبار مباشرة (بدون وحدة تحكم)
    app.MapGet("/api/test", () =>
    {
        logger.LogInformation("تم الوصول إلى نقطة نهاية الاختبار");
        return Results.Ok(new { message = "API is working!", timestamp = DateTime.UtcNow });
    });

    // فحص وإنشاء المجلدات اللازمة بمعالجة أخطاء أفضل
    try
    {
        string wwwrootPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
        logger.LogInformation($"مسار wwwroot: {wwwrootPath}");

        if (!Directory.Exists(wwwrootPath))
        {
            Directory.CreateDirectory(wwwrootPath);
            logger.LogInformation("تم إنشاء مجلد wwwroot");
        }

        string imagesPath = Path.Combine(wwwrootPath, "images");
        if (!Directory.Exists(imagesPath))
        {
            Directory.CreateDirectory(imagesPath);
            logger.LogInformation("تم إنشاء مجلد الصور");
        }

        string invoicesPath = Path.Combine(wwwrootPath, "invoices");
        if (!Directory.Exists(invoicesPath))
        {
            Directory.CreateDirectory(invoicesPath);
            logger.LogInformation("تم إنشاء مجلد الفواتير");
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "حدث خطأ أثناء إنشاء المجلدات ولكن سيستمر التطبيق");
    }

    app.UseDeveloperExceptionPage();

    // Configure Swagger
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "React Online Store API v1");
        c.RoutePrefix = string.Empty;
    });

    // إضافة دعم الملفات الثابتة بمعالجة أخطاء أفضل
    try
    {
        app.UseStaticFiles();

        // تكوين مجلد الصور مع إضافة رؤوس CORS
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "images")),
            RequestPath = "/images",
            OnPrepareResponse = ctx =>
            {
                // إضافة رؤوس CORS للسماح بالوصول من أي مصدر
                ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
                // إضافة التخزين المؤقت لتحسين الأداء
                ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=86400");
            }
        });

        // تكوين مجلد الفواتير مع إضافة رؤوس CORS أيضًا
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "invoices")),
            RequestPath = "/invoices",
            OnPrepareResponse = ctx =>
            {
                ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
                ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=86400");
            }
        });
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "حدث خطأ أثناء تكوين الملفات الثابتة ولكن سيستمر التطبيق");
    }

    // Basic health check endpoint
    app.MapGet("/health", () => Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow }));

    // معالجة أخطاء محسنة - تسجيل الأخطاء مع تفاصيل أكثر
    app.Use(async (context, next) =>
    {
        try
        {
            await next();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "خطأ غير معالج: {Path}", context.Request.Path);

            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";

            var exceptionDetails = new
            {
                error = "An error occurred",
                message = app.Environment.IsDevelopment() ? ex.Message : "Internal server error",
                path = context.Request.Path,
                timestamp = DateTime.UtcNow
            };

            await context.Response.WriteAsJsonAsync(exceptionDetails);
        }
    });

    app.UseHttpsRedirection();
    app.UseRouting();
    app.UseCors("AllowReactApp");
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    // تعديل: محاولة تهيئة قاعدة البيانات مع معالجة أفضل للأخطاء
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        logger.LogInformation("محاولة اختبار الاتصال بقاعدة البيانات");

        try
        {
            var context = services.GetRequiredService<ApplicationDbContext>();

            // اختبار الاتصال بقاعدة البيانات فقط
            var canConnect = await context.Database.CanConnectAsync();
            logger.LogInformation($"يمكن الاتصال بقاعدة البيانات: {canConnect}");

            if (canConnect)
            {
                logger.LogInformation("محاولة تهيئة المستخدمين والأدوار");
                var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
                var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

                // إضافة الأدوار الأساسية
                if (!await roleManager.RoleExistsAsync("admin"))
                {
                    logger.LogInformation("إنشاء دور المدير");
                    await roleManager.CreateAsync(new IdentityRole("admin"));
                }

                if (!await roleManager.RoleExistsAsync("user"))
                {
                    logger.LogInformation("إنشاء دور المستخدم");
                    await roleManager.CreateAsync(new IdentityRole("user"));
                }

                // إضافة حساب المدير التجريبي
                if (!await userManager.Users.AnyAsync(u => u.Email == "admin@example.com"))
                {
                    logger.LogInformation("إنشاء حساب المدير التجريبي");
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
                        logger.LogInformation("إضافة دور المدير للمستخدم");
                        await userManager.AddToRoleAsync(adminUser, "admin");
                    }
                    else
                    {
                        foreach (var error in result.Errors)
                        {
                            logger.LogWarning("خطأ في إنشاء المدير: {Code} - {Description}", error.Code, error.Description);
                        }
                    }
                }

                // إضافة حساب المستخدم التجريبي
                if (!await userManager.Users.AnyAsync(u => u.Email == "user@example.com"))
                {
                    logger.LogInformation("إنشاء حساب المستخدم التجريبي");
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
                        logger.LogInformation("إضافة دور المستخدم");
                        await userManager.AddToRoleAsync(normalUser, "user");
                    }
                    else
                    {
                        foreach (var error in result.Errors)
                        {
                            logger.LogWarning("خطأ في إنشاء المستخدم: {Code} - {Description}", error.Code, error.Description);
                        }
                    }
                }
            }
            else
            {
                logger.LogWarning("لا يمكن الاتصال بقاعدة البيانات، تخطي تهيئة المستخدمين");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "حدث خطأ أثناء تهيئة قاعدة البيانات");
        }
    }

    logger.LogInformation("بدء تشغيل التطبيق");
    app.Run();
}
catch (Exception ex)
{
    logger.LogCritical(ex, "حدث خطأ كارثي أثناء بدء التشغيل");
    throw;
}