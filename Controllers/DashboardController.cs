using System;
using System.Security.Claims;
using System.Threading.Tasks;
using AzureFinOps.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AzureFinOps.API.Controllers
{
    [AllowAnonymous]
    [Route("api/[controller]")]
    [ApiController]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;

        public DashboardController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary(
            [FromQuery] string? subscription,
            [FromQuery] string? resourceGroup,
            [FromQuery] string? service,
            [FromQuery] string? location,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var role = User.FindFirstValue(ClaimTypes.Role);

            if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId) || role == null)
            {
                userId = Guid.Empty;
                role = "Admin";
            }

            var summary = await _dashboardService.GetSummaryAsync(
                userId, role, subscription, resourceGroup, service, location, startDate, endDate);
            return Ok(summary);
        }
    }
}
