using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AzureFinOps.API.Data;
using AzureFinOps.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AzureFinOps.API.Services
{
    public interface IScopeService
    {
        Task<IEnumerable<string>> GetUserScopesAsync(Guid userId);
        
        // Scope Filtering Methods
        Task<IQueryable<AzureCostUsage>> ApplyScopeFilterAsync(IQueryable<AzureCostUsage> query, Guid userId, string role);
        Task<IQueryable<Budget>> ApplyScopeFilterAsync(IQueryable<Budget> query, Guid userId, string role);
        Task<IQueryable<ResourceGroup>> ApplyScopeFilterAsync(IQueryable<ResourceGroup> query, Guid userId, string role);
        Task<IQueryable<Subscription>> ApplyScopeFilterAsync(IQueryable<Subscription> query, Guid userId, string role);
    }

    public class ScopeService : IScopeService
    {
        private readonly ApplicationDbContext _context;

        public ScopeService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<string>> GetUserScopesAsync(Guid userId)
        {
            var scopes = await _context.UserScopes
                .Where(us => us.UserId == userId)
                .Select(us => us.ScopeValue)
                .ToListAsync();

            return scopes;
        }

        public async Task<IQueryable<AzureCostUsage>> ApplyScopeFilterAsync(IQueryable<AzureCostUsage> query, Guid userId, string role)
        {
            if (role == "Admin" || role == "Super Admin") return query;

            var scopes = await _context.UserScopes.Where(us => us.UserId == userId).ToListAsync();
            var subScopes = scopes.Where(s => s.ScopeType == "Subscription").Select(s => s.ScopeValue).ToList();
            var rgScopes = scopes.Where(s => s.ScopeType == "ResourceGroup").Select(s => s.ScopeValue).ToList();

            return query.Where(q => 
                (subScopes.Contains(q.SubscriptionName)) || 
                (rgScopes.Contains(q.ResourceGroup))
            );
        }

        public async Task<IQueryable<Budget>> ApplyScopeFilterAsync(IQueryable<Budget> query, Guid userId, string role)
        {
            if (role == "Admin" || role == "Super Admin") return query;

            var scopes = await _context.UserScopes.Where(us => us.UserId == userId).ToListAsync();
            var rgScopes = scopes.Where(s => s.ScopeType == "ResourceGroup").Select(s => s.ScopeValue).ToList();
            // Assuming budgets only apply to ResourceGroup level in this design constraint
            return query.Where(q => rgScopes.Contains(q.ResourceGroup));
        }

        public async Task<IQueryable<ResourceGroup>> ApplyScopeFilterAsync(IQueryable<ResourceGroup> query, Guid userId, string role)
        {
            if (role == "Admin" || role == "Super Admin") return query;

            var scopes = await _context.UserScopes.Where(us => us.UserId == userId).ToListAsync();
            var subIds = scopes.Where(s => s.ScopeType == "Subscription").Select(s => s.ScopeValue).ToList();
            var rgNames = scopes.Where(s => s.ScopeType == "ResourceGroup").Select(s => s.ScopeValue).ToList();

            return query.Include(q => q.Subscription).Where(q => 
                (subIds.Contains(q.Subscription.SubscriptionId)) || 
                (rgNames.Contains(q.ResourceGroupName))
            );
        }
        
        public async Task<IQueryable<Subscription>> ApplyScopeFilterAsync(IQueryable<Subscription> query, Guid userId, string role)
        {
            if (role == "Admin" || role == "Super Admin") return query;

            var scopes = await _context.UserScopes.Where(us => us.UserId == userId).ToListAsync();
            var subIds = scopes.Where(s => s.ScopeType == "Subscription").Select(s => s.ScopeValue).ToList();
            var rgNames = scopes.Where(s => s.ScopeType == "ResourceGroup").Select(s => s.ScopeValue).ToList();

            // A user can see subscriptions they are directly scoped to, or subscriptions containing RGs they are scoped to.
            var subsFromRgs = await _context.ResourceGroups
                .Where(rg => rgNames.Contains(rg.ResourceGroupName))
                .Select(rg => rg.SubscriptionId)
                .ToListAsync();

            var validInternalSubIds = await _context.Subscriptions
                .Where(s => subIds.Contains(s.SubscriptionId))
                .Select(s => s.Id)
                .ToListAsync();
                
            var combinedAccessibleInternalSubIds = validInternalSubIds.Union(subsFromRgs).Distinct().ToList();

            return query.Where(q => combinedAccessibleInternalSubIds.Contains(q.Id));
        }
    }
}
