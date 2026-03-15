using System.Collections.Generic;

namespace AzureFinOps.API.DTOs
{
    public class DashboardSummaryDto
    {
        public decimal TotalCostThisMonth { get; set; }
        public decimal TotalCostThisYear { get; set; }
        public decimal AverageDailyCost { get; set; }
        public int ActiveResourcesCount { get; set; }
    }

    public class CostByResourceGroupDto
    {
        public string ResourceGroup { get; set; } = string.Empty;
        public decimal TotalCost { get; set; }
    }

    public class CostByServiceDto
    {
        public string ServiceName { get; set; } = string.Empty;
        public decimal TotalCost { get; set; }
    }

    public class DailyCostTrendDto
    {
        public string Date { get; set; } = string.Empty;
        public decimal TotalCost { get; set; }
    }

    public class CostBySubscriptionDto
    {
        public string SubscriptionName { get; set; } = string.Empty;
        public decimal TotalCost { get; set; }
    }

    public class LookupDataDto
    {
        public IEnumerable<string> Subscriptions { get; set; } = new List<string>();
        public IEnumerable<string> ResourceGroups { get; set; } = new List<string>();
        public IEnumerable<string> Services { get; set; } = new List<string>();
        public IEnumerable<string> Locations { get; set; } = new List<string>();
    }

    public class TopResourceDto
    {
        public string ResourceName { get; set; } = string.Empty;
        public decimal TotalCost { get; set; }
    }
}
