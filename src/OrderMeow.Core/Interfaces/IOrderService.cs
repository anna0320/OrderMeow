using OrderMeow.Core.DTO;
using OrderMeow.Core.Enums;

namespace OrderMeow.Core.Interfaces;

public interface IOrderService
{
    Task<Guid> CreateOrderAsync(OrderDto orderDto, Guid userId);
    Task<List<OrderResponseDto>> GetAllOrdersAsync(Guid userId);
    Task<OrderResponseDto> GetOrderByIdAsync(Guid orderId, Guid userId);
    Task UpdateOrderAsync(OrderDto orderDto, Guid orderId, Guid userId);
    Task DeleteOrderAsync(Guid orderId, Guid userId);
    Task SetOrderStatusAsync(Guid orderId, Guid userId, OrderStatus status);
}