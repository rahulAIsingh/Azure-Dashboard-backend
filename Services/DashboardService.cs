using System;
using System.Linq;
using System.Threading.Tasks;
using AzureFinOps.API.Data;
using AzureFinOps.API.DTOs;
using Microsoft.EntityFrameworkCore;

namespace AzureFinOps.API.Services
{
    public interface IDashboardService
    {
        Task<DashboardSummaryDto> GetSummaryAsync(
            Guid userId, string role,
            string? subscription = null,
            string? resourceGroup = null,
            string? service = null,
            string? location = null,
            DateTime? startDate = null,
            DateTime? endDate = null);
    }

    public class DashboardService : IDashboardService
    {
        private readonly ApplicationDbContext _context;
        private readonly IScopeService _scopeService;

        public DashboardService(ApplicationDbContext context, IScopeService scopeService)
        {
            _context = context;
            _scopeService = scopeService;
        }

        public async Task<DashboardSummaryDto> GetSummaryAsync(
            Guid userId, string role,
            string? subscription = null,
            string? resourceGroup = null,
            string? service = null,
            string? location = null,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            // Base query with scope (admin sees all, viewer sees their assigned RGs)
            var query = _context.AzureCostUsage.AsQueryable();
            query = await _scopeService.ApplyScopeFilterAsync(query, userId, role);

            // Apply dashboard filters — same logic as /cost/records
            if (!string.IsNullOrEmpty(subscription))
                query = query.Where(c => c.SubscriptionName != null && c.SubscriptionName.ToLower() == subscription.ToLower());

            if (!string.IsNullOrEmpty(resourceGroup))
                query = query.Where(c => c.ResourceGroup != null && c.ResourceGroup.ToLower() == resourceGroup.ToLower());

            if (!string.IsNullOrEmpty(service))
                query = query.Where(c => c.ServiceName != null && c.ServiceName.ToLower() == service.ToLower());

            if (!string.IsNullOrEmpty(location))
                query = query.Where(c => c.Location != null && c.Location.ToLower() == location.ToLower());

            // Determine effective date window
            var now = DateTime.UtcNow;
            var effectiveStart = startDate ?? new DateTime(now.Year, now.Month, 1);
            var effectiveEnd = endDate ?? now;

            // KPI: Cost within the selected date range (for context/other uses if needed)
            var totalCostInRange = await query
                .Where(c => c.UsageDate >= effectiveStart && c.UsageDate <= effectiveEnd)
                .SumAsync(c => c.Cost);

            // KPI: Cost this calendar month (actual current month OR selected month for context)
            var startOfMonth = new DateTime(now.Year, now.Month, 1);
            var endOfMonthCalc = now;

            if (startDate.HasValue)
            {
                startOfMonth = new DateTime(startDate.Value.Year, startDate.Value.Month, 1);
                endOfMonthCalc = startOfMonth.AddMonths(1).AddDays(-1);
                
                // If it's the current month, don't show future projected cost if we don't have it
                if (startOfMonth.Year == now.Year && startOfMonth.Month == now.Month)
                {
                    endOfMonthCalc = now;
                }
            }

            var totalCostMonth = await query
                .Where(c => c.UsageDate >= startOfMonth && c.UsageDate <= endOfMonthCalc)
                .SumAsync(c => c.Cost);

            // KPI: Cost this calendar year (scoped to the same filters)
            var startOfYear = new DateTime(startOfMonth.Year, 1, 1);
            var endOfYearCalc = startOfMonth.Year == now.Year ? now : new DateTime(startOfMonth.Year, 12, 31);
            
            var totalCostYear = await query
                .Where(c => c.UsageDate >= startOfYear && c.UsageDate <= endOfYearCalc)
                .SumAsync(c => c.Cost);

            // KPI: Average daily cost over the selected date window
            var uniqueDatesInRange = await query
                .Where(c => c.UsageDate >= effectiveStart && c.UsageDate <= effectiveEnd)
                .Select(c => c.UsageDate)
                .Distinct()
                .CountAsync();
            var avgDaily = uniqueDatesInRange > 0 ? totalCostInRange / uniqueDatesInRange : 0;

            // KPI: Active unique resources in the selected date window
            var activeResourcesInRange = await query
                .Where(c => c.UsageDate >= effectiveStart && c.UsageDate <= effectiveEnd)
                .Select(c => c.ResourceName)
                .Distinct()
                .CountAsync();

            return new DashboardSummaryDto
            {
                TotalCostThisMonth = totalCostMonth,
                TotalCostThisYear = totalCostYear,
                AverageDailyCost = avgDaily,
                ActiveResourcesCount = activeResourcesInRange
            };
        }
    }
}
