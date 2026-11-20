using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using SmartDelivery.AuthService.Infrastructure.Data;
using SmartDelivery.CatalogService.Infrastructure.Data;

namespace SmartDelivery.IntegrationTests;

public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
{
    private readonly string _authDbName = $"AuthTestDb_{Guid.NewGuid():N}";
    private readonly string _catalogDbName = $"CatalogTestDb_{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Replace AuthDbContext with InMemory
            var authDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AuthDbContext>));
            if (authDescriptor != null) services.Remove(authDescriptor);

            services.AddDbContext<AuthDbContext>(options =>
            {
                options.UseInMemoryDatabase(_authDbName);
            });

            // Replace CatalogDbContext with InMemory
            var catalogDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<CatalogDbContext>));
            if (catalogDescriptor != null) services.Remove(catalogDescriptor);

            services.AddDbContext<CatalogDbContext>(options =>
            {
                options.UseInMemoryDatabase(_catalogDbName);
            });

            // Replace Redis cache with MemoryCache
            var cacheDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IDistributedCache));
            if (cacheDescriptor != null) services.Remove(cacheDescriptor);

            services.AddDistributedMemoryCache();
        });
    }
}
