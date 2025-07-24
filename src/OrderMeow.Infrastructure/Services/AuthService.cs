using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OrderMeow.App.Interfaces;
using OrderMeow.Infrastructure.Persistence;
using OrderMeow.Shared.DTO.Auth;

namespace OrderMeow.Infrastructure.Services;

public class AuthService: IAuthService
{
    private readonly ITokenService _tokenService;
    private readonly AppDbContext _dbContext;

    public AuthService(ITokenService tokenService, AppDbContext dbContext)
    {
        _tokenService = tokenService;
        _dbContext = dbContext;
    }

    public async Task<TokenDto> RefreshTokenAsync(TokenDto tokenDto)
    {
        var principal = _tokenService.GetClaimsPrincipalFromExpiredToken(tokenDto.Access_token);
        if (principal == null)
        {
            throw new SecurityTokenException("Invalid access token");
        }
        var userIdValue = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdValue) || !Guid.TryParse(userIdValue, out var userId))
        {
            throw new SecurityTokenException("Invalid user ID in token");
        }
        var user = await _dbContext.Users.SingleOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            throw new SecurityTokenException("User not found");
        }
        var storedRefreshToken = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => 
                rt.Token == tokenDto.Refresh_token.Token &&
                rt.UserId == userId);
        if (storedRefreshToken == null || !storedRefreshToken.IsActive)
        {
            throw new SecurityTokenException("Invalid refresh token");
        }
        storedRefreshToken.Revoked = DateTime.Now;
        
        var newRefreshToken = await _tokenService.GenerateAndSaveRefreshTokenAsync(user);
         _dbContext.RefreshTokens.Add(newRefreshToken);
        
        var newAccessToken = _tokenService.GenerateAccessToken(user);
        await _dbContext.SaveChangesAsync();
        return new TokenDto
        {
            Access_token = newAccessToken,
            Refresh_token = newRefreshToken
        };
    }
}