using System;
using System.Collections.Generic;

namespace AzureFinOps.API.DTOs
{
    public class UserManageDto
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Password { get; set; }
        public Guid RoleId { get; set; }
        public string? Department { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class UserScopeManageDto
    {
        public string ScopeType { get; set; } = string.Empty;
        public string ScopeValue { get; set; } = string.Empty;
    }

    public class UserRequestDto
    {
        public UserManageDto User { get; set; } = new UserManageDto();
        public IEnumerable<UserScopeManageDto> Scopes { get; set; } = new List<UserScopeManageDto>();
    }

    public class UserResponseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public Guid RoleId { get; set; }
        public string? Department { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? LastActive { get; set; }
        public IEnumerable<UserScopeManageDto> Scopes { get; set; } = new List<UserScopeManageDto>();
    }
}
