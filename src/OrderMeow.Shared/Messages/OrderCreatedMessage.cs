namespace OrderMeow.Shared.Messages;

public class OrderCreatedMessage
{
    public Guid OrderId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Title { get; set; } = null!;
}