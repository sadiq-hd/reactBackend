// Program.cs
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using reactBackend.Data;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure DbContext
var connectionString = builder.Environment.IsDevelopment()
    ? builder.Configuration.GetConnectionString("LocalConnection")
    : builder.Configuration.GetConnectionString("AzureConnection");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString,
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
        }));

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        builder =>
        {
            builder
                .WithOrigins(
                    "http://localhost:5173",
                    "https://recat-onlinestore.netlify.app"
                )
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
});

// Configure Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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

// Configure HTTP request pipeline
// إضافة Swagger في الإنتاج أيضاً
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "React Online Store API");
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowReactApp");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();