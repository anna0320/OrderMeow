using OrderMeow.Domain.Entities;
using OrderMeow.Shared.Messages;

namespace OrderMeow.App.Interfaces;

public interface IMessageQueueService
{
    Task PublishOrderCreatedAsync(OrderCreatedMessage  orderCreatedMessage);
}