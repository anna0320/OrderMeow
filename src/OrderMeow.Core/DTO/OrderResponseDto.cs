namespace OrderMeow.Core.DTO;

public class OrderResponseDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } =  null!;
}