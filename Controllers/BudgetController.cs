using System;
using System.Security.Claims;
using System.Threading.Tasks;
using AzureFinOps.API.Models;
using AzureFinOps.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AzureFinOps.API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class BudgetController : ControllerBase
    {
        private readonly IBudgetService _budgetService;

        public BudgetController(IBudgetService budgetService)
        {
            _budgetService = budgetService;
        }
        
        private (Guid userId, string role) GetUserClaims()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var role = User.FindFirstValue(ClaimTypes.Role);

            if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId) || role == null)
            {
                throw new UnauthorizedAccessException();
            }

            return (userId, role);
        }

        [HttpGet]
        public async Task<IActionResult> GetBudgets()
        {
            try {
                var (userId, role) = GetUserClaims();
                var budgets = await _budgetService.GetBudgetsAsync(userId, role);
                return Ok(budgets);
            } catch (UnauthorizedAccessException) {
                return Unauthorized();
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateBudget([FromBody] Budget budget)
        {
            try {
                var (userId, role) = GetUserClaims();
                var createdBudget = await _budgetService.CreateBudgetAsync(budget, userId, role);
                return CreatedAtAction(nameof(GetBudgets), new { id = createdBudget.Id }, createdBudget);
            } 
            catch (UnauthorizedAccessException ex) {
                return Forbid(ex.Message);
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateBudget(Guid id, [FromBody] Budget budget)
        {
            try {
                var (userId, role) = GetUserClaims();
                var updatedBudget = await _budgetService.UpdateBudgetAsync(id, budget, userId, role);
                
                if (updatedBudget == null) return NotFound();
                
                return Ok(updatedBudget);
            } catch (UnauthorizedAccessException) {
                return Unauthorized();
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBudget(Guid id)
        {
            try {
                var (userId, role) = GetUserClaims();
                var result = await _budgetService.DeleteBudgetAsync(id, userId, role);
                
                if (!result) return NotFound();
                
                return NoContent();
            } catch (UnauthorizedAccessException) {
                return Unauthorized();
            }
        }
    }
}
