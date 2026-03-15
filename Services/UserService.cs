using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AzureFinOps.API.Data;
using AzureFinOps.API.DTOs;
using AzureFinOps.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AzureFinOps.API.Services
{
    public interface IUserService
    {
        Task<IEnumerable<UserResponseDto>> GetUsersAsync();
        Task<IEnumerable<Role>> GetRolesAsync();
        Task<UserResponseDto> CreateUserAsync(User user, IEnumerable<UserScope> userScopes);
        Task<UserResponseDto?> UpdateUserAsync(Guid id, User user, IEnumerable<UserScope> userScopes);
        Task<bool> DeleteUserAsync(Guid id);
    }

    public class UserService : IUserService
    {
        private readonly ApplicationDbContext _context;

        public UserService(ApplicationDbContext context)
        {
            _context = context;
        }

        private string HashPassword(string password)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }

        private UserResponseDto MapToDto(User user)
        {
            return new UserResponseDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Role = user.Role?.Name ?? "Viewer",
                RoleId = user.RoleId,
                Department = user.Department,
                IsActive = user.IsActive,
                LastActive = user.LastActive,
                Scopes = user.UserScopes?.Select(s => new UserScopeManageDto
                {
                    ScopeType = s.ScopeType,
                    ScopeValue = s.ScopeValue
                }) ?? new List<UserScopeManageDto>()
            };
        }

        public async Task<IEnumerable<UserResponseDto>> GetUsersAsync()
        {
            var users = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.UserScopes)
                .ToListAsync();

            return users.Select(MapToDto);
        }

        public async Task<IEnumerable<Role>> GetRolesAsync()
        {
            return await _context.Roles.ToListAsync();
        }

        public async Task<UserResponseDto> CreateUserAsync(User user, IEnumerable<UserScope> userScopes)
        {
            user.Id = Guid.NewGuid();
            user.CreatedDate = DateTime.UtcNow;
            
            // Hash password before saving
            if (!string.IsNullOrEmpty(user.PasswordHash))
            {
                user.PasswordHash = HashPassword(user.PasswordHash);
            }
            
            _context.Users.Add(user);
            
            foreach(var scope in userScopes)
            {
                scope.UserId = user.Id;
                _context.UserScopes.Add(scope);
            }

            await _context.SaveChangesAsync();

            // Load navigation properties for mapping
            await _context.Entry(user).Reference(u => u.Role).LoadAsync();
            await _context.Entry(user).Collection(u => u.UserScopes).LoadAsync();

            return MapToDto(user);
        }

        public async Task<UserResponseDto?> UpdateUserAsync(Guid id, User updatedUser, IEnumerable<UserScope> updatedScopes)
        {
            var existingUser = await _context.Users
                .Include(u => u.UserScopes)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (existingUser == null) return null;

            existingUser.Name = updatedUser.Name;
            existingUser.Email = updatedUser.Email;
            existingUser.RoleId = updatedUser.RoleId;
            existingUser.IsActive = updatedUser.IsActive;
            existingUser.Department = updatedUser.Department;

            // Update password only if passed
            if (!string.IsNullOrEmpty(updatedUser.PasswordHash))
            {
                existingUser.PasswordHash = HashPassword(updatedUser.PasswordHash);
            }

            // Update scopes
            _context.UserScopes.RemoveRange(existingUser.UserScopes);

            foreach (var scope in updatedScopes)
            {
                scope.UserId = existingUser.Id;
                scope.Id = Guid.Empty; // Reset so EF adds new records
                _context.UserScopes.Add(scope);
            }

            _context.Users.Update(existingUser);
            await _context.SaveChangesAsync();

            // Load navigation properties for mapping
            await _context.Entry(existingUser).Reference(u => u.Role).LoadAsync();
            await _context.Entry(existingUser).Collection(u => u.UserScopes).LoadAsync();

            return MapToDto(existingUser);
        }

        public async Task<bool> DeleteUserAsync(Guid id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return false;

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return true;
        }
    }
}
