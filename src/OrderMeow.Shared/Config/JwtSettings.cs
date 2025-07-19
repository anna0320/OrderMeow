namespace OrderMeow.Shared.Config;

public class JwtSettings
{
    public string SecretKey { get; set; } = default;
    public string Issuer { get; set; } = default;
    public string Audience { get; set; } = default;
    public int ExpiryMinutes { get; set; } = default;
}