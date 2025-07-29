using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OrderMeow.Core.DTO.Auth;
using OrderMeow.Core.Entities;
using OrderMeow.Core.Interfaces;
using OrderMeow.Infrastructure.Config;
using OrderMeow.Infrastructure.Persistence;

namespace OrderMeow.Infrastructure.Services;

public class JwtService: IJwtService
{
    private readonly JwtSettings _jwtSettings;
    private readonly AppDbContext  _dbContext;

    public JwtService(IOptions<JwtSettings> jwtSettings, AppDbContext dbContext)
    {
        _dbContext = dbContext;
        _jwtSettings = jwtSettings.Value;
    }

    public string GenerateAccessToken(User user)
    {
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
                new Claim(ClaimTypes.Role, user.Role.ToString())
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

    public Task<RefreshToken> GenerateRefreshTokenAsync(User user)
    {
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);

        return Task.FromResult(new RefreshToken
        {
            Token = Convert.ToBase64String(randomNumber),
            Expires = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays),
            Created = DateTime.UtcNow,
            UserId = user.Id
        });
    }

    public async Task<TokenDto> RefreshTokenPairAsync(TokenDto tokenDto)
    {
        if (tokenDto.Refresh_token == null || string.IsNullOrWhiteSpace(tokenDto.Access_token))
        {
            throw new ArgumentException("Invalid token data");
        }
        
        var principal = GetPrincipalFromToken(tokenDto.Access_token) 
                        ?? throw new SecurityTokenException("Invalid access token");
        
        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (!Guid.TryParse(userId, out var userIdGuid))
        {
            throw new SecurityTokenException("Invalid user ID format");
        }
        
        await using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var user = await _dbContext.Users
                           .AsNoTracking()
                           .FirstOrDefaultAsync(u => u.Id == userIdGuid)
                       ?? throw new SecurityTokenException("User not found");
            
            var tokenRole = principal.FindFirst(ClaimTypes.Role)?.Value;
            if (user.Role.ToString() != tokenRole)
            {
                throw new SecurityTokenException("Role mismatch");
            }
            
            var storedRefreshToken = await _dbContext.RefreshTokens
                                         .FirstOrDefaultAsync(rt => 
                                             rt.Token == tokenDto.Refresh_token.Token &&
                                             rt.UserId == userIdGuid) 
                                     ?? throw new SecurityTokenException("Invalid refresh token");

            if (!ValidateRefreshToken(storedRefreshToken))
            {
                throw new SecurityTokenException("Refresh token is expired or revoked");
            }
            
            storedRefreshToken.Revoked = DateTime.UtcNow;
            var newRefreshToken = await GenerateRefreshTokenAsync(user);
            var newAccessToken = GenerateAccessToken(user);

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            return new TokenDto
            {
                Access_token = newAccessToken,
                Refresh_token = newRefreshToken
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public ClaimsPrincipal? GetPrincipalFromToken(string token)
    {
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = false,
            ValidateIssuer = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey)),
            ValidateLifetime = false
        };
        var tokenHandler = new JwtSecurityTokenHandler();
        var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var securityToken);
        if (securityToken is not JwtSecurityToken jwtSecurityToken ||
            jwtSecurityToken.Header.Alg != SecurityAlgorithms.HmacSha256)
        {
            throw new SecurityTokenException("Invalid token");
        }
        return principal ?? null;
    }

    private static bool ValidateRefreshToken(RefreshToken token)
    {
        return token.IsActive && 
               token.Expires > DateTime.UtcNow;
    }

    public async Task InvalidateUserTokensAsync(Guid userId)
    {
        await _dbContext.RefreshTokens
            .Where(rt => rt.UserId == userId)
            .ExecuteDeleteAsync();
    }
}