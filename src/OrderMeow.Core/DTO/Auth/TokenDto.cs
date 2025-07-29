using OrderMeow.Core.Entities;

namespace OrderMeow.Core.DTO.Auth;

public class TokenDto
{
    public string Access_token { get; set; }
    public RefreshToken Refresh_token { get; set; }
}