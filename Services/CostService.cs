using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AzureFinOps.API.Data;
using AzureFinOps.API.DTOs;
using AzureFinOps.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AzureFinOps.API.Services
{
    public interface ICostService
    {
        Task<IEnumerable<CostByResourceGroupDto>> GetCostByResourceGroupAsync(string? subscription, string? resourceGroup, string? service, string? location, DateTime? startDate, DateTime? endDate, Guid userId, string role);
        Task<IEnumerable<CostByServiceDto>> GetCostByServiceAsync(string? subscription, string? resourceGroup, string? service, string? location, DateTime? startDate, DateTime? endDate, Guid userId, string role);
        Task<IEnumerable<DailyCostTrendDto>> GetDailyTrendAsync(string? subscription, string? resourceGroup, string? service, string? location, DateTime? startDate, DateTime? endDate, Guid userId, string role);
        Task<IEnumerable<CostBySubscriptionDto>> GetCostBySubscriptionAsync(string? subscription, string? resourceGroup, string? service, string? location, DateTime? startDate, DateTime? endDate, Guid userId, string role);
        Task<object> GetResourceGroupDetailAsync(string resourceGroup, Guid userId, string role);
        Task<IEnumerable<object>> GetFilteredRecordsAsync(DateTime? startDate, DateTime? endDate, string? resourceGroup, string? service, string? subscription, string? location, Guid userId, string role);
        Task<IEnumerable<TopResourceDto>> GetTopResourcesAsync(int limit, string? subscription, string? resourceGroup, string? service, string? location, DateTime? startDate, DateTime? endDate, Guid userId, string role);
        Task<LookupDataDto> GetLookupsAsync(string? subscription, Guid userId, string role);
    }

    public class CostService : ICostService
    {
        private readonly ApplicationDbContext _context;
        private readonly IScopeService _scopeService;

        public CostService(ApplicationDbContext context, IScopeService scopeService)
        {
            _context = context;
            _scopeService = scopeService;
        }

        private async Task<IQueryable<AzureCostUsage>> GetBaseFilteredQuery(string? subscription, string? resourceGroup, string? service, string? location, DateTime? startDate, DateTime? endDate, Guid userId, string role)
        {
            var query = await _scopeService.ApplyScopeFilterAsync(_context.AzureCostUsage.AsQueryable(), userId, role);

            if (!string.IsNullOrEmpty(subscription) && subscription != "All")
                query = query.Where(c => c.SubscriptionName != null && c.SubscriptionName.ToLower() == subscription.ToLower());
            
            if (!string.IsNullOrEmpty(resourceGroup) && resourceGroup != "All")
                query = query.Where(c => c.ResourceGroup != null && c.ResourceGroup.ToLower() == resourceGroup.ToLower());

            if (!string.IsNullOrEmpty(service) && service != "All")
                query = query.Where(c => c.ServiceName != null && c.ServiceName.ToLower() == service.ToLower());

            if (!string.IsNullOrEmpty(location) && location != "All")
                query = query.Where(c => c.Location != null && c.Location.ToLower() == location.ToLower());

            if (startDate.HasValue)
                query = query.Where(c => c.UsageDate >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(c => c.UsageDate <= endDate.Value);

            return query;
        }

        public async Task<IEnumerable<CostByResourceGroupDto>> GetCostByResourceGroupAsync(string? subscription, string? resourceGroup, string? service, string? location, DateTime? startDate, DateTime? endDate, Guid userId, string role)
        {
            var query = await GetBaseFilteredQuery(subscription, resourceGroup, service, location, startDate, endDate, userId, role);
            
            return await query
                .GroupBy(c => c.ResourceGroup)
                .Select(g => new CostByResourceGroupDto
                {
                    ResourceGroup = g.Key,
                    TotalCost = g.Sum(c => c.Cost)
                })
                .ToListAsync();
        }

        public async Task<IEnumerable<CostByServiceDto>> GetCostByServiceAsync(string? subscription, string? resourceGroup, string? service, string? location, DateTime? startDate, DateTime? endDate, Guid userId, string role)
        {
            var query = await GetBaseFilteredQuery(subscription, resourceGroup, service, location, startDate, endDate, userId, role);
            
            return await query
                .Where(c => c.ServiceName != null)
                .GroupBy(c => c.ServiceName)
                .Select(g => new CostByServiceDto
                {
                    ServiceName = g.Key!,
                    TotalCost = g.Sum(c => c.Cost)
                })
                .ToListAsync();
        }

        public async Task<IEnumerable<DailyCostTrendDto>> GetDailyTrendAsync(string? subscription, string? resourceGroup, string? service, string? location, DateTime? startDate, DateTime? endDate, Guid userId, string role)
        {
            var query = await GetBaseFilteredQuery(subscription, resourceGroup, service, location, startDate, endDate, userId, role);
            
            return await query
                .GroupBy(c => c.UsageDate)
                .OrderBy(g => g.Key)
                .Select(g => new DailyCostTrendDto
                {
                    Date = g.Key.ToString("yyyy-MM-dd"),
                    TotalCost = g.Sum(c => c.Cost)
                })
                .ToListAsync();
        }

        public async Task<IEnumerable<CostBySubscriptionDto>> GetCostBySubscriptionAsync(string? subscription, string? resourceGroup, string? service, string? location, DateTime? startDate, DateTime? endDate, Guid userId, string role)
        {
            var query = await GetBaseFilteredQuery(subscription, resourceGroup, service, location, startDate, endDate, userId, role);
            
            return await query
                .GroupBy(c => c.SubscriptionName)
                .Select(g => new CostBySubscriptionDto
                {
                    SubscriptionName = g.Key,
                    TotalCost = g.Sum(c => c.Cost)
                })
                .ToListAsync();
        }

        public async Task<object> GetResourceGroupDetailAsync(string resourceGroup, Guid userId, string role)
        {
            var query = await _scopeService.ApplyScopeFilterAsync(_context.AzureCostUsage.AsQueryable(), userId, role);
            
            var details = await query
                .Where(c => c.ResourceGroup != null && c.ResourceGroup.ToLower() == resourceGroup.ToLower())
                .Select(c => new
                {
                    resourceName = c.ResourceName,
                    resourceType = c.ResourceType,
                    serviceName = c.ServiceName,
                    cost = c.Cost,
                    usageDate = c.UsageDate.ToString("yyyy-MM-dd")
                })
                .ToListAsync();

            return details;
        }

        public async Task<IEnumerable<object>> GetFilteredRecordsAsync(DateTime? startDate, DateTime? endDate, string? resourceGroup, string? service, string? subscription, string? location, Guid userId, string role)
        {
            var query = await GetBaseFilteredQuery(subscription, resourceGroup, service, location, startDate, endDate, userId, role);

            return await query.Select(c => new
            {
                id = c.Id.ToString(),
                usageDate = c.UsageDate.ToString("yyyy-MM-dd"),
                subscriptionName = c.SubscriptionName,
                resourceGroup = c.ResourceGroup,
                resourceName = c.ResourceName,
                resourceType = c.ResourceType,
                serviceName = c.ServiceName,
                resourcePlan = c.ResourcePlan,
                meterCategory = c.MeterCategory,
                location = c.Location,
                cost = c.Cost
            }).ToListAsync();
        }

        public async Task<IEnumerable<TopResourceDto>> GetTopResourcesAsync(int limit, string? subscription, string? resourceGroup, string? service, string? location, DateTime? startDate, DateTime? endDate, Guid userId, string role)
        {
            var query = await GetBaseFilteredQuery(subscription, resourceGroup, service, location, startDate, endDate, userId, role);

            return await query
                .GroupBy(c => c.ResourceName)
                .Select(g => new TopResourceDto
                {
                    ResourceName = g.Key,
                    TotalCost = g.Sum(c => c.Cost)
                })
                .OrderByDescending(r => r.TotalCost)
                .Take(limit)
                .ToListAsync();
        }
        public async Task<LookupDataDto> GetLookupsAsync(string? subscription, Guid userId, string role)
        {
            var query = await _scopeService.ApplyScopeFilterAsync(_context.AzureCostUsage.AsQueryable(), userId, role);

            if (!string.IsNullOrEmpty(subscription) && subscription != "All")
            {
                query = query.Where(c => c.SubscriptionName != null && c.SubscriptionName.ToLower() == subscription.ToLower());
            }
            
            var subscriptions = await query.Where(c => c.SubscriptionName != null && c.SubscriptionName != "").Select(c => c.SubscriptionName).Distinct().ToListAsync();
            var resourceGroups = await query.Where(c => c.ResourceGroup != null && c.ResourceGroup != "").Select(c => c.ResourceGroup).Distinct().ToListAsync();
            var services = await query.Where(c => c.ServiceName != null && c.ServiceName != "").Select(c => c.ServiceName!).Distinct().ToListAsync();
            var locations = await query.Where(c => c.Location != null && c.Location != "").Select(c => c.Location!).Distinct().ToListAsync();

            return new LookupDataDto
            {
                Subscriptions = subscriptions,
                ResourceGroups = resourceGroups,
                Services = services,
                Locations = locations
            };
        }
    }
}
