using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AzureFinOps.API.Data;
using AzureFinOps.API.Models;

namespace AzureFinOps.API.Utilities
{
    public static class DatabaseSeeder
    {
        public static async Task SeedAsync(ApplicationDbContext context)
        {
            // Seed Roles
            if (!context.Roles.Any())
            {
                context.Roles.AddRange(new List<Role>
                {
                    new Role { Name = "Super Admin" },
                    new Role { Name = "Admin" },
                    new Role { Name = "Editor" },
                    new Role { Name = "Viewer" }
                });
                await context.SaveChangesAsync();
            }

            // Seed initial Admin User
            if (!context.Users.Any())
            {
                var adminRole = context.Roles.First(r => r.Name == "Admin");
                context.Users.Add(new User
                {
                    Name = "Admin User",
                    Email = "admin@company.com",
                    PasswordHash = HashPassword("admin123"),
                    RoleId = adminRole.Id,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                });
                await context.SaveChangesAsync();
            }
        }

        private static string HashPassword(string password)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }

        public static async Task ClearAllDataAsync(ApplicationDbContext context)
        {
            // Clear cost usage, resource groups, subscriptions, and processed files log
            // This ensures a clean slate for real data ingestion
            context.AzureCostUsage.RemoveRange(context.AzureCostUsage);
            context.ResourceGroups.RemoveRange(context.ResourceGroups);
            context.Subscriptions.RemoveRange(context.Subscriptions);
            context.ProcessedFiles.RemoveRange(context.ProcessedFiles);
            
            await context.SaveChangesAsync();
        }
    }
}
