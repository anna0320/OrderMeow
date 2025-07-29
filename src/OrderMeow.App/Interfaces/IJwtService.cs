using System.Security.Claims;
using OrderMeow.Domain.Entities;
using OrderMeow.Shared.DTO.Auth;

namespace OrderMeow.App.Interfaces;

public interface IJwtService
{
    string GenerateAccessToken(User user);
    Task<RefreshToken> GenerateRefreshTokenAsync(User user);
    Task InvalidateUserTokensAsync(Guid userId);
}