using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MockQueryable.Moq;
using Moq;
using OrderMeow.Core.DTO;
using OrderMeow.Core.Entities;
using OrderMeow.Core.Interfaces;
using OrderMeow.Infrastructure.Messages;
using OrderMeow.Infrastructure.Persistence;
using OrderMeow.Infrastructure.Services;

namespace OrderMeow.Tests;

public class OrderServiceTests
{
    private readonly Mock<AppDbContext> _mockDbContext;
    private readonly Mock<IMessageQueueService> _mockMessageQueueService;
    private readonly Mock<ICacheService> _mockCacheService;
    private readonly OrderService _orderService;

    public OrderServiceTests()
    {
        _mockDbContext = new Mock<AppDbContext>();
        _mockMessageQueueService = new Mock<IMessageQueueService>();
        _mockCacheService = new Mock<ICacheService>();
        _orderService = new OrderService(
            _mockMessageQueueService.Object,
            _mockDbContext.Object,
            _mockCacheService.Object);
    }

    [Fact]
    public async Task CreateOrderAsync_ValidDate_ReturnsOrderIdAndSaves()
    {
        //Arrange
        var order = new OrderDto
        {
            Title = "Test Title",
            Description = "Test Description"
        };
        var userId = Guid.NewGuid();
        var mockDbSet = new Mock<DbSet<Order>>();
        _mockDbContext.Setup(db => db.Orders).Returns(mockDbSet.Object);
        _mockDbContext.Setup(db => db.Database.BeginTransactionAsync(CancellationToken.None))
            .ReturnsAsync(Mock.Of<IDbContextTransaction>());

        //Act
        var orderId = await _orderService.CreateOrderAsync(order, userId);

        //Assert
        Assert.NotEqual(Guid.Empty, orderId);
        mockDbSet.Verify(db => db.Add(It.Is<Order>(o =>
                o.Title == order.Title && o.UserId == userId)),
            Times.Once);
        _mockDbContext.Verify(db => db.SaveChangesAsync(CancellationToken.None), Times.Once);
        _mockMessageQueueService.Verify(mq => mq.PublishOrderCreatedAsync(It.IsAny<OrderCreatedMessage>()), Times.Once);
        _mockCacheService.Verify(c => c.RemoveAsync(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task CreateOrderAsync_OnException_RollsBackTransaction()
    {
        //Arrange
        _mockDbContext.Setup(db => db.SaveChangesAsync(CancellationToken.None)).ThrowsAsync(new Exception("DB error!"));

        //Act and Assert
        await Assert.ThrowsAsync<Exception>(() =>
            _orderService.CreateOrderAsync(new OrderDto(), Guid.NewGuid()));
        _mockDbContext.Verify(db => db.Database.BeginTransactionAsync(CancellationToken.None), Times.Once);
        _mockDbContext.Verify(db => db.SaveChangesAsync(CancellationToken.None), Times.Once);
        _mockCacheService.Verify(c => c.RemoveAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetAllOrdersAsync_ReturnsCachedData_IfExists()
    {
        //Arrange
        var userId = Guid.NewGuid();
        var cachedOrders = new List<OrderResponseDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Title = "Test Title",
                Description = "Test Description"
            }
        };
        _mockCacheService.Setup(c => c.GetAsync<List<OrderResponseDto>>(It.IsAny<string>()))
            .ReturnsAsync(cachedOrders);

        //Act
        var result = await _orderService.GetAllOrdersAsync(userId);

        //Assert
        Assert.Single(result);
        Assert.Equal(cachedOrders[0].Id, result[0].Id);
        _mockDbContext.Verify(db => db.Orders, Times.Never);
    }

    [Fact]
    public async Task GetAllOrdersAsync_QueriesDb_WhenCacheEmpty()
    {
        //Arrange
        var userId = Guid.NewGuid();
        var dbOrders = new List<Order>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = "Test Title",
                Description = "Test Description"
            }
        };
        var mockDbSet = dbOrders.BuildMockDbSet();
        _mockDbContext.Setup(db => db.Orders).Returns(mockDbSet.Object);
        
        //Act
        var result = await _orderService.GetAllOrdersAsync(userId);
        
        //Assert
        Assert.Single(result);
        _mockCacheService.Verify( c => 
            c.SetAsync(
                It.IsAny<string>(), 
                It.IsAny<List<OrderResponseDto>>(), 
                It.IsAny<TimeSpan>()), 
            Times.Once);
    }
}