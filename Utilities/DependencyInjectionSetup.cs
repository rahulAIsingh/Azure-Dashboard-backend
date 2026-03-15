using Microsoft.EntityFrameworkCore;
using AzureFinOps.API.Data;
using AzureFinOps.API.Services;
using Microsoft.Extensions.Configuration;

namespace AzureFinOps.API.Utilities
{
    public static class DependencyInjectionSetup
    {
        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Register DbContext
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

            // Register Services
            services.AddScoped<IScopeService, ScopeService>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IDashboardService, DashboardService>();
            services.AddScoped<ICostService, CostService>();
            services.AddScoped<IBudgetService, BudgetService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<ISubscriptionService, SubscriptionService>();
            services.AddScoped<ICostAnomalyService, CostAnomalyService>();
            services.AddScoped<IManualPricingImportService, ManualPricingImportService>();

            // Register Repositories
            // services.AddScoped<IUserRepository, UserRepository>();

            return services;
        }
    }
}
