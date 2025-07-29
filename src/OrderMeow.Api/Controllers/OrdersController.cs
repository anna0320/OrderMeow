using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderMeow.Core.DTO;
using OrderMeow.Core.Interfaces;

namespace OrderMeow.Controllers;
[ApiController]
[Route("orders")]
[Authorize]
public class OrdersController: ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    private Guid GetUserId()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var result))
        {
            throw new UnauthorizedAccessException("Invalid user ID in token");
        }
        return result;
    }
    
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] OrderDto order)
    {
        var id = await _orderService.CreateOrderAsync(order,  GetUserId());
        return Ok(new { id });
    }
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var orders = await _orderService.GetAllOrdersAsync(GetUserId());
        return Ok(orders);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById( Guid id)
    {
        var order = await _orderService.GetOrderByIdAsync(id, GetUserId());
        return Ok(order);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update([FromBody] OrderDto orderDto, Guid id)
    {
        await _orderService.UpdateOrderAsync(orderDto,id, GetUserId());
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _orderService.DeleteOrderAsync(id, GetUserId());
        return NoContent();
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> ChangeOrderStatus(Guid id, UpdateOrderStatusDto status)
    {
        await _orderService.SetOrderStatusAsync(id, GetUserId(),  status.Status);
        return NoContent();
    }
}