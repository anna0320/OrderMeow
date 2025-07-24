using OrderMeow.Shared.DTO.Auth;

namespace OrderMeow.App.Interfaces;

public interface IAuthService
{
    Task<TokenDto> RefreshTokenAsync(TokenDto tokenDto);
}