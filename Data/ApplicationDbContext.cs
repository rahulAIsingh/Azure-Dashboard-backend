using AzureFinOps.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AzureFinOps.API.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<UserScope> UserScopes { get; set; }
        public DbSet<Subscription> Subscriptions { get; set; }
        public DbSet<ResourceGroup> ResourceGroups { get; set; }
        public DbSet<AzureCostUsage> AzureCostUsage { get; set; }
        public DbSet<Budget> Budgets { get; set; }
        public DbSet<Alert> Alerts { get; set; }
        public DbSet<ProcessedFile> ProcessedFiles { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Configure unique index for User Email
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // Configure unique index for SubscriptionId
            modelBuilder.Entity<Subscription>()
                .HasIndex(s => s.SubscriptionId)
                .IsUnique();

            // Configure relationships
            modelBuilder.Entity<User>()
                .HasOne(u => u.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(u => u.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UserScope>()
                .HasOne(us => us.User)
                .WithMany(u => u.UserScopes)
                .HasForeignKey(us => us.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ResourceGroup>()
                .HasOne(rg => rg.Subscription)
                .WithMany(s => s.ResourceGroups)
                .HasForeignKey(rg => rg.SubscriptionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes for Cost Analytics filtering
            modelBuilder.Entity<AzureCostUsage>()
                .HasIndex(a => a.UsageDate);
            modelBuilder.Entity<AzureCostUsage>()
                .HasIndex(a => a.SubscriptionName);
            modelBuilder.Entity<AzureCostUsage>()
                .HasIndex(a => a.ResourceGroup);
                
            modelBuilder.Entity<Budget>()
                .HasIndex(b => b.ResourceGroup);
                
            modelBuilder.Entity<Alert>()
                .HasIndex(a => a.ResourceGroup);
        }
    }
}
