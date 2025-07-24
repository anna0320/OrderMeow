using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderMeow.App.Interfaces;
using OrderMeow.Domain.Entities;
using OrderMeow.Infrastructure.Persistence;
using OrderMeow.Shared.DTO.Auth;

namespace OrderMeow.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly AppDbContext _dbContext;
    private readonly ITokenService _tokenService;
    private readonly IAuthService _authService;
    private readonly ILogger<UserService> _logger;

    public UserService(
        AppDbContext dbContext,
        ILogger<UserService> logger, ITokenService tokenService, IAuthService authService)
    {
        _dbContext = dbContext;
        _logger = logger;
        _tokenService = tokenService;
        _authService = authService;
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
                Id = Guid.NewGuid(),
                Username = registerDto.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(registerDto.Password),
            };
            
             var accessToken = _tokenService.GenerateAccessToken(user);
             var refreshToken = await _tokenService.GenerateAndSaveRefreshTokenAsync(user);
             user.RefreshTokens.Add(refreshToken);
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
            
            return new AuthResponseDto
            {
                AccessToken = accessToken,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                RefreshToken = refreshToken.Token,
                RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(7)
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
            
            var refreshToken = await _dbContext.RefreshTokens.FirstOrDefaultAsync(rt=>rt.UserId == user.Id);
            if (refreshToken == null)
            {
                throw new ApplicationException("Refresh token not found");
            }

            var tokenDto = new TokenDto
            {
                Access_token = _tokenService.GenerateAccessToken(user),
                Refresh_token = refreshToken
            };
            var refreshTokenChanged = await _authService.RefreshTokenAsync(tokenDto);
            
            return new AuthResponseDto
            {
                AccessToken = refreshTokenChanged.Access_token,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                RefreshToken = refreshTokenChanged.Refresh_token.Token,
                RefreshTokenExpiresAt = refreshToken.Expires
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            throw;
        }
    }
}