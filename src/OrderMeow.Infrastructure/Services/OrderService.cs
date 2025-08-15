using Microsoft.EntityFrameworkCore;
using OrderMeow.Core.DTO;
using OrderMeow.Core.Entities;
using OrderMeow.Core.Enums;
using OrderMeow.Core.Interfaces;
using OrderMeow.Infrastructure.Messages;
using OrderMeow.Infrastructure.Persistence;

namespace OrderMeow.Infrastructure.Services;

public class OrderService: IOrderService
{
    private readonly AppDbContext _dbContext;
    private readonly IMessageQueueService _messageQueueService;
    private readonly ICacheService _cacheService;

    public OrderService(
        IMessageQueueService messageQueueService, 
        AppDbContext dbContext, 
        ICacheService cacheService)
    {
        _messageQueueService = messageQueueService;
        _dbContext = dbContext;
        _cacheService = cacheService;
    }

    public async Task<Guid> CreateOrderAsync(OrderDto orderDto, Guid userId)
    {
        var order = new Order
        {
            Title = orderDto.Title,
            Description = orderDto.Description,
            CreatedAt = DateTime.UtcNow,
            UserId = userId,
            Status = OrderStatus.Created
        };
        
        await using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            _dbContext.Orders.Add(order);
            await _dbContext.SaveChangesAsync();

            var message = new OrderCreatedMessage
            {
                OrderId = order.Id,
                UserId = order.UserId,
                CreatedAt = order.CreatedAt,
                Title = order.Title
            };

            await _messageQueueService.PublishOrderCreatedAsync(message);
            await InvalidateUserOrdersCache(userId, order.Id);
            
            await transaction.CommitAsync();
            return order.Id;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<List<OrderResponseDto>> GetAllOrdersAsync(Guid userId)
    {
        var cacheKey = _cacheService.GetCacheKey(userId);
        var response = await _cacheService.GetOrCreateAsync(
            cacheKey,
            async () => await GetOrdersFromDbAsync(userId),
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(30));
        if (response is null)
        {
            throw new NullReferenceException();
        }
        return response;
    }

    private async Task<List<OrderResponseDto>> GetOrdersFromDbAsync(Guid userId)
    {
        return await _dbContext.Orders
            .Where(o => o.UserId == userId)
            .AsNoTracking()
            .Select(o => new OrderResponseDto
            {
                Id = o.Id,
                Title = o.Title,
                Description = o.Description,
                CreatedAt = o.CreatedAt,
                Status = o.Status.ToString(),
            }).ToListAsync();
    }

    public async Task<OrderResponseDto> GetOrderByIdAsync(Guid orderId, Guid userId)
    {
        var response = await GetAllOrdersAsync(userId);
        var order = response.FirstOrDefault(o => o.Id == orderId);
        if (order is null)
        {
            throw new Exception("Order not found");
        }
        return order;
    }

    public async Task UpdateOrderAsync(OrderDto orderDto, Guid orderId, Guid userId)
    {
        var order = await _dbContext.Orders.FindAsync(orderId);
        if (order == null ||  order.UserId != userId)
        {
            throw new Exception("Order not found");
        }

        order.Title = orderDto.Title;
        order.Description = orderDto.Description;

        await _dbContext.SaveChangesAsync();
        await InvalidateUserOrdersCache(userId, orderId);
    }

    public async Task DeleteOrderAsync(Guid orderId, Guid userId)
    {
        var order = await _dbContext.Orders.FindAsync(orderId);
        if (order == null || order.UserId != userId)
        {
            throw new Exception("Order not found");
        }

        _dbContext.Orders.Remove(order);
        await _dbContext.SaveChangesAsync();
        await _cacheService.RemoveAsync(_cacheService.GetCacheKey(userId));
    }

    public async Task SetOrderStatusAsync(Guid orderId, Guid userId, OrderStatus status)
    {
        var affectedRows = await _dbContext.Orders
            .Where(o=>o.Id == orderId && o.UserId == userId)
            .ExecuteUpdateAsync(s=> 
                s.SetProperty(o=>o.Status, status));
        
        if (affectedRows == 0)
        {
            throw new Exception("Order not found");
        }
        
        await _cacheService.RemoveAsync(_cacheService.GetCacheKey(userId));
    }
    
    private async Task InvalidateUserOrdersCache(Guid userId, Guid? orderId = null)
    {
        var userOrdersKey = _cacheService.GetCacheKey(userId);
        await _cacheService.RemoveAsync(userOrdersKey);
        if (orderId.HasValue)
        {
            var singleOrderKey = $"order_{orderId.Value}_{userId}";
            await _cacheService.RemoveAsync(singleOrderKey);
        }
    }
}