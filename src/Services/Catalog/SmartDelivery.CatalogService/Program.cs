using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using SmartDelivery.CatalogService.Application.DTOs;
using SmartDelivery.CatalogService.Domain;
using SmartDelivery.CatalogService.Grpc;
using SmartDelivery.CatalogService.Infrastructure.Data;
using SmartDelivery.CatalogService.Infrastructure.Services;
using SmartDelivery.Shared.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrEmpty(connectionString))
{
    builder.Services.AddDbContext<CatalogDbContext>(options =>
        options.UseNpgsql(connectionString));
}
else
{
    builder.Services.AddDbContext<CatalogDbContext>(options =>
        options.UseInMemoryDatabase("CatalogDb"));
}

// Redis
var redisConn = builder.Configuration["Redis:ConnectionString"];
if (!string.IsNullOrEmpty(redisConn))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConn;
        options.InstanceName = "CatalogService_";
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

// Services
builder.Services.AddScoped<ICatalogService, CatalogService>();

// gRPC
builder.Services.AddGrpc();

// Auth
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = JwtTokenHelper.GetTokenValidationParameters(jwtSettings);
    });

builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "SmartDelivery CatalogService API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapGrpcService<StockGrpcServiceImpl>();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "CatalogService" }));

// Seed initial data
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    try
    {
        db.Database.EnsureCreated();
        if (!db.Categories.Any())
        {
            var electronics = new Category(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Electronics", "Electronic gadgets and devices");
            var groceries = new Category(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Groceries", "Fresh food and essentials");
            db.Categories.AddRange(electronics, groceries);

            var sampleProduct = new Product(
                Guid.Parse("33333333-3333-3333-3333-333333333333"),
                "Apple iPhone 15",
                "Flagship smartphone with 128GB storage",
                799.99m,
                electronics.Id,
                Guid.Parse("44444444-4444-4444-4444-444444444444")
            );
            var sampleInventory = new InventoryItem(Guid.NewGuid(), sampleProduct.Id, "WH-MAIN-01", 50);
            sampleProduct.AttachInventory(sampleInventory);

            db.Products.Add(sampleProduct);
            db.InventoryItems.Add(sampleInventory);
            db.SaveChanges();
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Could not ensure database created/seeded automatically.");
    }
}

app.Run();

namespace SmartDelivery.CatalogService
{
    public partial class Program { }
}
