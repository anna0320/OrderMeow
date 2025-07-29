namespace OrderMeow.Infrastructure.Config;

public class RabbitMqSettings
{
    public string HostName { get; set; } =  "localhost";
    public string UserName { get; set; } = "rmuser";
    public string Password { get; set; } = "rmpassword";
    public string VirtualHost { get; set; } = "ordermeow";
    public int Port { get; set; } = 5672;
    public string QueueName { get; set; } = "orders-queue";
}