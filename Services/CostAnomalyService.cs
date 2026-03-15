using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AzureFinOps.API.Data;
using AzureFinOps.API.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AzureFinOps.API.Services
{
    public class CostAnomalyService : ICostAnomalyService
    {
        private readonly ApplicationDbContext _context;
        private readonly IScopeService _scopeService;
        private readonly ILogger<CostAnomalyService> _logger;

        public CostAnomalyService(
            ApplicationDbContext context, 
            IScopeService scopeService,
            ILogger<CostAnomalyService> logger)
        {
            _context = context;
            _scopeService = scopeService;
            _logger = logger;
        }

        public async Task<IEnumerable<CostAnomalyDto>> GetCostAnomaliesAsync(DateTime startDate, DateTime endDate, Guid userId, string role)
        {
            // Apply scope filtering
            var baseQuery = await _scopeService.ApplyScopeFilterAsync(_context.AzureCostUsage, userId, role);

            // Group cost by date
            var dailyCosts = await baseQuery
                .Where(c => c.UsageDate >= startDate.AddDays(-7) && c.UsageDate <= endDate)
                .GroupBy(c => c.UsageDate.Date)
                .Select(g => new { Date = g.Key, TotalCost = g.Sum(c => c.Cost) })
                .OrderBy(g => g.Date)
                .ToListAsync();

            var result = new List<CostAnomalyDto>();

            // Calculate rolling average and detect anomalies
            for (int i = 0; i < dailyCosts.Count; i++)
            {
                var current = dailyCosts[i];
                if (current.Date < startDate) continue;

                // Get previous 7 days for rolling average
                var previous7Days = dailyCosts
                    .Where(d => d.Date < current.Date && d.Date >= current.Date.AddDays(-7))
                    .ToList();

                decimal averageCost = previous7Days.Any() ? previous7Days.Average(d => d.TotalCost) : current.TotalCost;
                bool isAnomaly = averageCost > 0 && current.TotalCost > (averageCost * 3);
                double deviation = averageCost > 0 ? (double)((current.TotalCost - averageCost) / averageCost) * 100 : 0;

                var dto = new CostAnomalyDto
                {
                    Date = current.Date,
                    Cost = current.TotalCost,
                    AverageCost = averageCost,
                    DeviationPercentage = Math.Round(deviation, 2),
                    IsAnomaly = isAnomaly
                };

                if (isAnomaly)
                {
                    _logger.LogWarning("Cost anomaly detected on {Date}. Cost: {Cost}, Average: {Average}", 
                        dto.Date.ToString("yyyy-MM-dd"), dto.Cost, dto.AverageCost);
                }

                result.Add(dto);
            }

            return result;
        }
    }
}
