using System;
using System.Security.Claims;
using System.Threading.Tasks;
using AzureFinOps.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AzureFinOps.API.Controllers
{
    [AllowAnonymous]
    [Route("api")]
    [ApiController]
    public class SubscriptionController : ControllerBase
    {
        private readonly ISubscriptionService _subscriptionService;

        public SubscriptionController(ISubscriptionService subscriptionService)
        {
            _subscriptionService = subscriptionService;
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

        [HttpGet("subscriptions")]
        public async Task<IActionResult> GetSubscriptions()
        {
            try {
                var (userId, role) = GetUserClaims();
                var subscriptions = await _subscriptionService.GetSubscriptionsAsync(userId, role);
                return Ok(subscriptions);
            } catch (UnauthorizedAccessException) {
                return Unauthorized();
            }
        }

        [HttpGet("resource-groups")]
        public async Task<IActionResult> GetResourceGroups()
        {
            try {
                var (userId, role) = GetUserClaims();
                var resourceGroups = await _subscriptionService.GetResourceGroupsAsync(userId, role);
                return Ok(resourceGroups);
            } catch (UnauthorizedAccessException) {
                return Unauthorized();
            }
        }
    }
}
