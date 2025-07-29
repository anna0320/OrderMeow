using OrderMeow.Core.Enums;

namespace OrderMeow.Core.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = null!;
    public string? PasswordHash { get; set; }
    public RoleType Role { get; set; }
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}