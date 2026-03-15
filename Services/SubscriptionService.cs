using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AzureFinOps.API.Data;
using AzureFinOps.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AzureFinOps.API.Services
{
    public interface ISubscriptionService
    {
        Task<IEnumerable<Subscription>> GetSubscriptionsAsync(Guid userId, string role);
        Task<IEnumerable<ResourceGroup>> GetResourceGroupsAsync(Guid userId, string role);
    }

    public class SubscriptionService : ISubscriptionService
    {
        private readonly ApplicationDbContext _context;
        private readonly IScopeService _scopeService;

        public SubscriptionService(ApplicationDbContext context, IScopeService scopeService)
        {
            _context = context;
            _scopeService = scopeService;
        }

        public async Task<IEnumerable<Subscription>> GetSubscriptionsAsync(Guid userId, string role)
        {
            var query = await _scopeService.ApplyScopeFilterAsync(_context.Subscriptions.AsQueryable(), userId, role);
            return await query.ToListAsync();
        }

        public async Task<IEnumerable<ResourceGroup>> GetResourceGroupsAsync(Guid userId, string role)
        {
            var query = await _scopeService.ApplyScopeFilterAsync(_context.ResourceGroups.AsQueryable(), userId, role);
            return await query.ToListAsync();
        }
    }
}
