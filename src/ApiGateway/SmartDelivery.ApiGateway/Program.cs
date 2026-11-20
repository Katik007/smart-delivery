var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "ApiGateway",
    student = "Кабанова Екатерина",
    domain = "kabanovae.9593647-gu32875.twc1.net",
    timestamp = DateTime.UtcNow
}));

app.MapReverseProxy();

app.Run();

namespace SmartDelivery.ApiGateway
{
    public partial class Program { }
}
