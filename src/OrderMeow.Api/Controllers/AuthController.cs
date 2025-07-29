using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderMeow.Core.DTO.Auth;
using OrderMeow.Core.Interfaces;

namespace OrderMeow.Controllers;
[ApiController]
[Route("api/[controller]")]
public class AuthController: ControllerBase
{
    private readonly IUserService _userService;
    private readonly IJwtService _jwtService;

    public AuthController(IUserService userService, IJwtService jwtService)
    {
        _userService = userService;
        _jwtService = jwtService;
    }
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> RegisterAsync([FromBody]RegisterDto registerDto)
    {
        return Ok(await _userService.RegisterAsync(registerDto));
    }
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> LoginAsync([FromBody] LoginDto loginDto)
    {
        var token = await _userService.LoginAsync(loginDto);
        return Ok(new
        {
            access_token = token.AccessToken, 
            token_type = "Bearer"
        });
    }
    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] TokenDto tokens)
    {
        var newTokens = await _jwtService.RefreshTokenPairAsync(tokens);
        return Ok(newTokens);
    }
}