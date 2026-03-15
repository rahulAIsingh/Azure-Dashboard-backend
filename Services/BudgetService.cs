using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AzureFinOps.API.Data;
using AzureFinOps.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AzureFinOps.API.Services
{
    public interface IBudgetService
    {
        Task<IEnumerable<Budget>> GetBudgetsAsync(Guid userId, string role);
        Task<Budget> CreateBudgetAsync(Budget budget, Guid userId, string role);
        Task<Budget?> UpdateBudgetAsync(Guid id, Budget budget, Guid userId, string role);
        Task<bool> DeleteBudgetAsync(Guid id, Guid userId, string role);
    }

    public class BudgetService : IBudgetService
    {
        private readonly ApplicationDbContext _context;
        private readonly IScopeService _scopeService;

        public BudgetService(ApplicationDbContext context, IScopeService scopeService)
        {
            _context = context;
            _scopeService = scopeService;
        }

        public async Task<IEnumerable<Budget>> GetBudgetsAsync(Guid userId, string role)
        {
            var query = await _scopeService.ApplyScopeFilterAsync(_context.Budgets.AsQueryable(), userId, role);
            return await query.ToListAsync();
        }

        public async Task<Budget> CreateBudgetAsync(Budget budget, Guid userId, string role)
        {
            // Verify user has access to create budget in this resource group
            var rgQuery = await _scopeService.ApplyScopeFilterAsync(_context.ResourceGroups.AsQueryable(), userId, role);
            var hasAccess = await rgQuery.AnyAsync(rg => rg.ResourceGroupName == budget.ResourceGroup);
            
            if (!hasAccess && role != "Admin") 
            {
                throw new UnauthorizedAccessException("You do not have permission to create a budget for this resource group.");
            }

            budget.Id = Guid.NewGuid();
            budget.CreatedDate = DateTime.UtcNow;
            
            _context.Budgets.Add(budget);
            await _context.SaveChangesAsync();
            
            return budget;
        }

        public async Task<Budget?> UpdateBudgetAsync(Guid id, Budget updatedBudget, Guid userId, string role)
        {
            var query = await _scopeService.ApplyScopeFilterAsync(_context.Budgets.AsQueryable(), userId, role);
            var existingBudget = await query.FirstOrDefaultAsync(b => b.Id == id);

            if (existingBudget == null) return null;

            existingBudget.MonthlyBudget = updatedBudget.MonthlyBudget;
            
            _context.Budgets.Update(existingBudget);
            await _context.SaveChangesAsync();

            return existingBudget;
        }

        public async Task<bool> DeleteBudgetAsync(Guid id, Guid userId, string role)
        {
            var query = await _scopeService.ApplyScopeFilterAsync(_context.Budgets.AsQueryable(), userId, role);
            var budget = await query.FirstOrDefaultAsync(b => b.Id == id);

            if (budget == null) return false;

            _context.Budgets.Remove(budget);
            await _context.SaveChangesAsync();

            return true;
        }
    }
}
