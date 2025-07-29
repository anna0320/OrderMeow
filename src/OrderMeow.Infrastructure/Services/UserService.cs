using System.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderMeow.Core.DTO.Auth;
using OrderMeow.Core.Entities;
using OrderMeow.Core.Interfaces;
using OrderMeow.Infrastructure.Persistence;

namespace OrderMeow.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly AppDbContext _dbContext;
    private readonly IJwtService _jwtService;
    private readonly ILogger<UserService> _logger;

    public UserService(
        AppDbContext dbContext, 
        IJwtService jwtService,
        ILogger<UserService> logger)
    {
        _dbContext = dbContext;
        _jwtService = jwtService;
        _logger = logger;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterDto registerDto)
    {
        if (registerDto == null)
        {
            throw new ArgumentNullException(nameof(registerDto));
        }

        if (string.IsNullOrWhiteSpace(registerDto.Password) || registerDto.Password.Length < 6)
        {
            throw new ArgumentException("Password must be at least 6 characters long");
        }
        
        var userExists = await _dbContext.Users
            .AnyAsync(x => x.Username == registerDto.Username);
        if (userExists)
        {
            throw new InvalidOperationException("User already exists");
        }
        
        var user = new User
        {
            Username = registerDto.Username.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(registerDto.Password),
        };
        
        var refreshToken = await _jwtService.GenerateRefreshTokenAsync(user);
        user.RefreshTokens.Add(refreshToken);
        var accessToken = _jwtService.GenerateAccessToken(user);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
        
            return new AuthResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken.Token,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                RefreshTokenExpiresAt = refreshToken.Expires
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error during user registration");
            throw;
        }
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto loginDto)
    {
        if (loginDto == null)
        {
            throw new ArgumentNullException(nameof(loginDto));
        }
        var delayTask = Task.Delay(200); 

        try
        {
            var user = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Username == loginDto.Username);
            await delayTask;

            if (user == null || !BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
            {
                throw new SecurityException("Invalid credentials");
            }
            
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                await _jwtService.InvalidateUserTokensAsync(user.Id);
                var accessToken = _jwtService.GenerateAccessToken(user);
                var refreshToken = await _jwtService.GenerateRefreshTokenAsync(user);
                
                _dbContext.RefreshTokens.Add(refreshToken);
                await _dbContext.SaveChangesAsync();
                
                await transaction.CommitAsync();
                _logger.LogInformation("User logged in: {Username}", user.Username);

                return new AuthResponseDto
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken.Token,
                    ExpiresAt = DateTime.UtcNow.AddHours(1),
                    RefreshTokenExpiresAt = refreshToken.Expires
                };
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed login attempt for {Username}", loginDto.Username);
            throw;
        }
    }
}