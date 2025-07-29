using Microsoft.EntityFrameworkCore;
using OrderMeow.App.Interfaces;
using OrderMeow.Domain.Entities;
using OrderMeow.Domain.Enums;
using OrderMeow.Infrastructure.Persistence;
using OrderMeow.Shared.DTO;
using OrderMeow.Shared.Messages;

namespace OrderMeow.Infrastructure.Services;

public class OrderService: IOrderService
{
    private readonly AppDbContext _dbContext;
    private readonly IMessageQueueService _messageQueueService;

    public OrderService(IMessageQueueService messageQueueService, AppDbContext dbContext)
    {
        _messageQueueService = messageQueueService;
        _dbContext = dbContext;
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
            })
            .ToListAsync();
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
        var order = await _dbContext.Orders
            .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);
            
        if (order == null)
        {
            throw new Exception("Order not found");
        }

        order.Title = orderDto.Title;
        order.Description = orderDto.Description;

        await _dbContext.SaveChangesAsync();
    }

    public async Task DeleteOrderAsync(Guid orderId, Guid userId)
    {
        var order = await _dbContext.Orders
            .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);
            
        if (order == null)
        {
            throw new Exception("Order not found");
        }

        _dbContext.Orders.Remove(order);
        await _dbContext.SaveChangesAsync();
    }

    public async Task SetOrderStatusAsync(Guid orderId, Guid userId, OrderStatus status)
    {
        var order = await _dbContext.Orders
            .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);
            
        if (order == null)
        {
            throw new Exception("Order not found");
        }

        order.Status = status;
        await _dbContext.SaveChangesAsync();
    }
}