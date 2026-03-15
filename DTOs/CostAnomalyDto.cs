using System;

namespace AzureFinOps.API.DTOs
{
    public class CostAnomalyDto
    {
        public DateTime Date { get; set; }
        public decimal Cost { get; set; }
        public decimal AverageCost { get; set; }
        public double DeviationPercentage { get; set; }
        public bool IsAnomaly { get; set; }
    }
}
