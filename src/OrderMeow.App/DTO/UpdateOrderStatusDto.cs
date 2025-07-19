using OrderMeow.Domain.Enums;

namespace OrderMeow.App.DTO;

public class UpdateOrderStatusDto
{
    public OrderStatus Status { get; set; }
}