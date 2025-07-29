using OrderMeow.Core.DTO.Auth;
using OrderMeow.Core.Entities;

namespace OrderMeow.Core.Interfaces;

public interface IJwtService
{
    string GenerateAccessToken(User user);
    Task<RefreshToken> GenerateRefreshTokenAsync(User user);
    Task<TokenDto> RefreshTokenPairAsync(TokenDto tokenDto);
    Task InvalidateUserTokensAsync(Guid userId);
}