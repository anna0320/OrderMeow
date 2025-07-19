using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OrderMeow.App.Interfaces;
using OrderMeow.Domain.Entities;
using OrderMeow.Infrastructure.Persistence;
using OrderMeow.Shared.Config;
using OrderMeow.Shared.DTO.Auth;

namespace OrderMeow.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly AppDbContext _dbContext;
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<UserService> _logger;

    public UserService(
        IOptions<JwtSettings> jwtSettings, 
        AppDbContext dbContext,
        ILogger<UserService> logger)
    {
        _dbContext = dbContext;
        _jwtSettings = jwtSettings.Value;
        _logger = logger;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterDto registerDto)
    {
        try
        {
            var userExists = await _dbContext.Users
                .AnyAsync(x => x.Username == registerDto.Username);

            if (userExists)
            {
                throw new ApplicationException("User already exists");
            }

            var user = new User
            {
                Username = registerDto.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(registerDto.Password),
            };
            
            await _dbContext.Users.AddAsync(user);
            await _dbContext.SaveChangesAsync();
            
            return new AuthResponseDto
            {
                Token = GenerateToken(user),
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration");
            throw;
        }
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto loginDto)
    {
        try
        {
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Username == loginDto.Username);

            if (user == null)
            {
                throw new ApplicationException("User not found");
            }

            if (!BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
            {
                throw new ApplicationException("Invalid password");
            }

            return new AuthResponseDto
            {
                Token = GenerateToken(user),
                ExpiresAt = DateTime.UtcNow.AddHours(1) 
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            throw;
        }
    }

    private string GenerateToken(User user)
    {
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.Username)
            ]),
            Issuer = _jwtSettings.Issuer,
            Audience = _jwtSettings.Audience,
            Expires = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey)), 
                SecurityAlgorithms.HmacSha256Signature)
        };
    
        var token = new JwtSecurityTokenHandler().CreateToken(tokenDescriptor);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}