using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderMeow.App.Interfaces;
using OrderMeow.Shared.DTO.Auth;

namespace OrderMeow.Controllers;
[ApiController]
[Route("api/[controller]")]
public class AuthController: ControllerBase
{
    private readonly IUserService _userService;

    public AuthController(IUserService userService)
    {
        _userService = userService;
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
}