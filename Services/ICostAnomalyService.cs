using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AzureFinOps.API.DTOs;

namespace AzureFinOps.API.Services
{
    public interface ICostAnomalyService
    {
        Task<IEnumerable<CostAnomalyDto>> GetCostAnomaliesAsync(DateTime startDate, DateTime endDate, Guid userId, string role);
    }
}
