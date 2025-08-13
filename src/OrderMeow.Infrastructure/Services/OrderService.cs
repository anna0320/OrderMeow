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
            await _cacheService.RemoveAsync(_cacheService.GetCacheKey(userId));
            
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
        var cachedOrders = await _cacheService.GetAsync<List<OrderResponseDto>>(cacheKey);
        if (cachedOrders is not null)
        {
            return cachedOrders;
        }
        
        var orders =  await _dbContext.Orders
            .Where(o => o.UserId == userId)
            .AsNoTracking()
            .Select(o => new OrderResponseDto
            {
                Id = o.Id,
                Title = o.Title,
                Description = o.Description,
                CreatedAt = o.CreatedAt,
                Status = o.Status.ToString(),
            })
            .ToListAsync();
        await _cacheService.SetAsync(cacheKey, orders, TimeSpan.FromHours(1));
        return orders;
    }

    public async Task<OrderResponseDto?> GetOrderByIdAsync(Guid orderId, Guid userId)
    {
        return await _dbContext.Orders
            .Where(o => o.Id == orderId && o.UserId == userId)
            .AsNoTracking()
            .Select(o => new OrderResponseDto
            {
                Id = o.Id,
                Title = o.Title,
                Description = o.Description,
                CreatedAt = o.CreatedAt,
                Status = o.Status.ToString(),
            })
            .FirstOrDefaultAsync();
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
        await _cacheService.RemoveAsync(_cacheService.GetCacheKey(userId));
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
}