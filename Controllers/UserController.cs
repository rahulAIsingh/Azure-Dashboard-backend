using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AzureFinOps.API.Models;
using AzureFinOps.API.Services;
using AzureFinOps.API.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AzureFinOps.API.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("api/users")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _userService.GetUsersAsync();
            return Ok(users);
        }

        [HttpGet("roles")]
        public async Task<IActionResult> GetRoles()
        {
            var roles = await _userService.GetRolesAsync();
            return Ok(roles);
        }

        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody] UserRequestDto request)
        {
            var user = new User
            {
                Name = request.User.Name,
                Email = request.User.Email,
                PasswordHash = request.User.Password ?? string.Empty,
                RoleId = request.User.RoleId,
                Department = request.User.Department,
                IsActive = request.User.IsActive
            };

            var scopes = request.Scopes.Select(s => new UserScope
            {
                ScopeType = s.ScopeType,
                ScopeValue = s.ScopeValue
            }).ToList();

            UserResponseDto createdUser = await _userService.CreateUserAsync(user, scopes);
            return CreatedAtAction(nameof(GetUsers), new { id = createdUser.Id }, createdUser);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UserRequestDto request)
        {
            var user = new User
            {
                Name = request.User.Name,
                Email = request.User.Email,
                PasswordHash = request.User.Password ?? string.Empty,
                RoleId = request.User.RoleId,
                Department = request.User.Department,
                IsActive = request.User.IsActive
            };

            var scopes = request.Scopes.Select(s => new UserScope
            {
                ScopeType = s.ScopeType,
                ScopeValue = s.ScopeValue
            }).ToList();

            UserResponseDto? updatedUser = await _userService.UpdateUserAsync(id, user, scopes);
            
            if (updatedUser == null) return NotFound();
            
            return Ok(updatedUser);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(Guid id)
        {
            var result = await _userService.DeleteUserAsync(id);
            
            if (!result) return NotFound();
            
            return NoContent();
        }
    }
}
