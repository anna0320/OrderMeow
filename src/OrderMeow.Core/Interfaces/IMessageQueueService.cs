using OrderMeow.Infrastructure.Messages;

namespace OrderMeow.Core.Interfaces;
public interface IMessageQueueService
{
    Task PublishOrderCreatedAsync(OrderCreatedMessage  orderCreatedMessage);
}