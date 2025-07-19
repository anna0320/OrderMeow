using OrderMeow.Domain.Enums;

namespace OrderMeow.Domain.Entities;
public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public OrderStatus Status { get; set; } =  OrderStatus.Created;
    public Guid UserId { get; set; }
}