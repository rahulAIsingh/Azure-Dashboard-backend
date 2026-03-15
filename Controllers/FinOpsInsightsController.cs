using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using AzureFinOps.API.DTOs;
using AzureFinOps.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AzureFinOps.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous] // Simplified for development, should use [Authorize] in production
    public class FinOpsInsightsController : ControllerBase
    {
        private readonly ICostAnomalyService _anomalyService;

        public FinOpsInsightsController(ICostAnomalyService anomalyService)
        {
            _anomalyService = anomalyService;
        }

        [HttpGet("anomalies")]
        public async Task<ActionResult<IEnumerable<CostAnomalyDto>>> GetAnomalies(
            [FromQuery] DateTime? startDate = null, 
            [FromQuery] DateTime? endDate = null)
        {
            var end = endDate ?? DateTime.UtcNow.Date;
            var start = startDate ?? end.AddDays(-30);

            var (userId, role) = GetUserClaims();
            var anomalies = await _anomalyService.GetCostAnomaliesAsync(start, end, userId, role);
            
            return Ok(anomalies);
        }

        private (Guid userId, string role) GetUserClaims()
        {
            // For development without actual tokens, fallback to Admin
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var role = User.FindFirstValue(ClaimTypes.Role) ?? "Admin";
            
            if (!Guid.TryParse(userIdStr, out Guid userId))
            {
                userId = Guid.Empty; // Default for dev
            }

            return (userId, role);
        }
    }
}
