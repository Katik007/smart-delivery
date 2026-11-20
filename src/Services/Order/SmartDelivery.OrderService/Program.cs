using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using SmartDelivery.OrderService.Application.DTOs;
using SmartDelivery.OrderService.Infrastructure.Clients;
using SmartDelivery.OrderService.Infrastructure.Data;
using SmartDelivery.OrderService.Infrastructure.Services;
using SmartDelivery.OrderService.Saga;
using SmartDelivery.OrderService.Workers;
using SmartDelivery.Shared.Messaging;
using SmartDelivery.Shared.Protos;
using SmartDelivery.Shared.Security;

var builder = WebApplication.CreateBuilder(args);

// JWT Settings
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();

// RabbitMQ Options & Publisher
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrEmpty(connectionString))
{
    builder.Services.AddDbContext<OrderDbContext>(options =>
        options.UseNpgsql(connectionString));
}
else
{
    builder.Services.AddDbContext<OrderDbContext>(options =>
        options.UseInMemoryDatabase("OrderDb"));
}

// gRPC Client to CatalogService
var catalogUrl = builder.Configuration["Services:CatalogService:Url"] ?? "http://localhost:5002";
builder.Services.AddGrpcClient<StockService.StockServiceClient>(o =>
{
    o.Address = new Uri(catalogUrl);
});

// Domain & Application Services
builder.Services.AddScoped<IStockServiceClient, StockGrpcServiceClient>();
builder.Services.AddScoped<IOrderSagaOrchestrator, OrderSagaOrchestrator>();
builder.Services.AddScoped<IOrderService, OrderService>();

// Outbox Background Worker
builder.Services.AddHostedService<OutboxPublisherWorker>();

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
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "SmartDelivery OrderService API", Version = "v1" });
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

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "OrderService" }));

// Ensure DB Created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    try
    {
        db.Database.EnsureCreated();
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Could not ensure database created automatically.");
    }
}

app.Run();

namespace SmartDelivery.OrderService
{
    public partial class Program { }
}
