using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SonicRelay.Infrastructure.Persistence;

namespace SonicRelay.Api.IntegrationTests;

public sealed class SonicRelayApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"sonicrelay-tests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Postgres", "Host=localhost;Database=sonicrelay_tests;Username=test;Password=test");
        builder.UseSetting("Redis:ConnectionString", "localhost:6379");
        builder.UseSetting("Sessions:CodeTtlMinutes", "10");
        builder.UseSetting("Sessions:CodeHmacKey", "integration-test-session-code-key");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
            services.RemoveAll<IDistributedCache>();
            services.AddDistributedMemoryCache();
        });
    }
}
