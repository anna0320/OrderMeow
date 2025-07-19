using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using OrderMeow.App.Interfaces;
using OrderMeow.Shared.Config;
using OrderMeow.Shared.Messages;
using RabbitMQ.Client;


namespace OrderMeow.Infrastructure.Services;

public class RabbitMqService: IMessageQueueService
{
    private readonly RabbitMqSettings _settings;
    private readonly ILogger<RabbitMqService> _logger;
    

    public RabbitMqService(IOptions<RabbitMqSettings> options, ILogger<RabbitMqService> logger)
    {
        _logger = logger;
        _settings = options.Value;
    }

    public async Task PublishOrderCreatedAsync(OrderCreatedMessage orderCreatedMessage)
    {
        var factory = new ConnectionFactory
        {
            HostName = _settings.HostName,
            Port = _settings.Port,
            VirtualHost = _settings.VirtualHost,
            UserName = _settings.UserName,
            Password = _settings.Password,
        };
        var connection = await factory.CreateConnectionAsync();
        var channel = await connection.CreateChannelAsync();
        await channel.QueueDeclareAsync(
            queue: _settings.QueueName,
            durable: true,
            exclusive:  false,
            autoDelete: false,
            arguments: null);
        var message = JsonConvert.SerializeObject(orderCreatedMessage);
        var body = Encoding.UTF8.GetBytes(message);
        
        _logger.LogInformation("Publishing message to RabbitMQ");
        await channel.BasicPublishAsync("", _settings.QueueName, body);
        
        await channel.CloseAsync();
        await connection.CloseAsync();
    }
}