using OrderMeow.Core.Enums;

namespace OrderMeow.Core.DTO;

public class UpdateOrderStatusDto
{
    public OrderStatus Status { get; set; }
}