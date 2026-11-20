using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using SmartDelivery.DeliveryService.Domain;
using SmartDelivery.DeliveryService.Infrastructure.Data;
using SmartDelivery.DeliveryService.Workers;
using SmartDelivery.Shared.Messaging;
using SmartDelivery.Shared.Security;

var builder = WebApplication.CreateBuilder(args);

// JWT Settings
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();

// RabbitMQ Options
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrEmpty(connectionString))
{
    builder.Services.AddDbContext<DeliveryDbContext>(options =>
        options.UseNpgsql(connectionString));
}
else
{
    builder.Services.AddDbContext<DeliveryDbContext>(options =>
        options.UseInMemoryDatabase("DeliveryDb"));
}

// Background RabbitMQ Consumer (with Inbox pattern)
builder.Services.AddHostedService<OrderPaidConsumerWorker>();

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
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "SmartDelivery DeliveryService API", Version = "v1" });
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
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "DeliveryService" }));

// Ensure DB Created & Seed initial couriers
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DeliveryDbContext>();
    try
    {
        db.Database.EnsureCreated();
        if (!db.Couriers.Any())
        {
            db.Couriers.AddRange(
                new Courier(Guid.NewGuid(), "Иван Смирнов", "+79991234567", VehicleType.Scooter, "Center"),
                new Courier(Guid.NewGuid(), "Алексей Кузнецов", "+79997654321", VehicleType.Bicycle, "North"),
                new Courier(Guid.NewGuid(), "Мария Попова", "+79998889900", VehicleType.Car, "South")
            );
            db.SaveChanges();
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Could not ensure database created automatically.");
    }
}

app.Run();

namespace SmartDelivery.DeliveryService
{
    public partial class Program { }
}
