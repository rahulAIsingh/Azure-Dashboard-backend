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
    public class CostController : ControllerBase
    {
        private readonly ICostService _costService;

        public CostController(ICostService costService)
        {
            _costService = costService;
        }

        private (Guid userId, string role) GetUserClaims()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var role = User.FindFirstValue(ClaimTypes.Role);

            if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId) || role == null)
            {
                // Fallback for development without token
                return (Guid.Empty, "Admin");
            }

            return (userId, role);
        }

        [HttpGet("by-resource-group")]
        public async Task<IActionResult> GetCostByResourceGroup([FromQuery] string? subscription, [FromQuery] string? resourceGroup, [FromQuery] string? service, [FromQuery] string? location, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            try {
                var (userId, role) = GetUserClaims();
                var data = await _costService.GetCostByResourceGroupAsync(subscription, resourceGroup, service, location, startDate, endDate, userId, role);
                return Ok(data);
            } catch (UnauthorizedAccessException) {
                return Unauthorized();
            }
        }

        [HttpGet("by-service")]
        public async Task<IActionResult> GetCostByService([FromQuery] string? subscription, [FromQuery] string? resourceGroup, [FromQuery] string? service, [FromQuery] string? location, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            try {
                var (userId, role) = GetUserClaims();
                var data = await _costService.GetCostByServiceAsync(subscription, resourceGroup, service, location, startDate, endDate, userId, role);
                return Ok(data);
            } catch (UnauthorizedAccessException) {
                return Unauthorized();
            }
        }

        [HttpGet("daily-trend")]
        public async Task<IActionResult> GetDailyTrend([FromQuery] string? subscription, [FromQuery] string? resourceGroup, [FromQuery] string? service, [FromQuery] string? location, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            try {
                var (userId, role) = GetUserClaims();
                var data = await _costService.GetDailyTrendAsync(subscription, resourceGroup, service, location, startDate, endDate, userId, role);
                return Ok(data);
            } catch (UnauthorizedAccessException) {
                return Unauthorized();
            }
        }

        [HttpGet("subscription-cost")]
        public async Task<IActionResult> GetCostBySubscription([FromQuery] string? subscription, [FromQuery] string? resourceGroup, [FromQuery] string? service, [FromQuery] string? location, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            try {
                var (userId, role) = GetUserClaims();
                var data = await _costService.GetCostBySubscriptionAsync(subscription, resourceGroup, service, location, startDate, endDate, userId, role);
                return Ok(data);
            } catch (UnauthorizedAccessException) {
                return Unauthorized();
            }
        }

        [HttpGet("resource-group-detail")]
        public async Task<IActionResult> GetResourceGroupDetail([FromQuery] string resourceGroup)
        {
            try {
                var (userId, role) = GetUserClaims();
                var data = await _costService.GetResourceGroupDetailAsync(resourceGroup, userId, role);
                return Ok(data);
            } catch (UnauthorizedAccessException) {
                return Unauthorized();
            }
        }
        [HttpGet("records")]
        public async Task<IActionResult> GetFilteredRecords([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, [FromQuery] string? resourceGroup, [FromQuery] string? service, [FromQuery] string? subscription, [FromQuery] string? location)
        {
            try {
                var (userId, role) = GetUserClaims();
                var data = await _costService.GetFilteredRecordsAsync(startDate, endDate, resourceGroup, service, subscription, location, userId, role);
                return Ok(data);
            } catch (UnauthorizedAccessException) {
                return Unauthorized();
            }
        }

        [HttpGet("top-resources")]
        public async Task<IActionResult> GetTopResources([FromQuery] int limit = 10, [FromQuery] string? subscription = null, [FromQuery] string? resourceGroup = null, [FromQuery] string? service = null, [FromQuery] string? location = null, [FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
        {
            try {
                var (userId, role) = GetUserClaims();
                var data = await _costService.GetTopResourcesAsync(limit, subscription, resourceGroup, service, location, startDate, endDate, userId, role);
                return Ok(data);
            } catch (UnauthorizedAccessException) {
                return Unauthorized();
            }
        }

        [HttpGet("lookups")]
        public async Task<IActionResult> GetLookups([FromQuery] string? subscription, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            try {
                var (userId, role) = GetUserClaims();
                var data = await _costService.GetLookupsAsync(subscription, startDate, endDate, userId, role);
                return Ok(data);
            } catch (UnauthorizedAccessException) {
                return Unauthorized();
            }
        }
    }
}
