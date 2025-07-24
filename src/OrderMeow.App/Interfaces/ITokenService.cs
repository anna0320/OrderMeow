using System.Security.Claims;
using OrderMeow.Domain.Entities;

namespace OrderMeow.App.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(User user);
    Task<RefreshToken> GenerateAndSaveRefreshTokenAsync(User user);
    ClaimsPrincipal? GetClaimsPrincipalFromExpiredToken(string token);
}