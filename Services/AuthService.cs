using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using AzureFinOps.API.Data;
using AzureFinOps.API.DTOs;
using AzureFinOps.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace AzureFinOps.API.Services
{
    public interface IAuthService
    {
        Task<User?> ValidateUserAsync(string email, string password);
        string GenerateJwtToken(User user, IEnumerable<string> scopes);
    }

    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthService(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<User?> ValidateUserAsync(string email, string password)
        {
            var hashedPassword = HashPassword(password);
            
            var user = await _context.Users
                .Include(u => u.Role)
                .SingleOrDefaultAsync(u => u.Email == email && u.PasswordHash == hashedPassword && u.IsActive);

            return user;
        }

        private string HashPassword(string password)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }

        public string GenerateJwtToken(User user, IEnumerable<string> scopes)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["Secret"] ?? throw new ArgumentNullException("JwtSettings:Secret is missing");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role.Name)
            };

            // Add scopes as claims (multiple claims with same type or a comma separated list, depending on policy need)
            foreach (var scope in scopes)
            {
                claims.Add(new Claim("Scope", scope));
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(double.Parse(jwtSettings["ExpirationMinutes"] ?? "60")),
                Issuer = jwtSettings["Issuer"],
                Audience = jwtSettings["Audience"],
                SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            
            return tokenHandler.WriteToken(token);
        }
    }
}
