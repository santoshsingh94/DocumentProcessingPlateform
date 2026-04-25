using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using DocumentProcessing.Api.Configuration;

namespace DocumentProcessing.Api.Messaging
{
    public class RabbitMqPublisher : IMessagePublisher, IDisposable
    {
        private readonly RabbitMqOptions _options;
        private readonly IConnection _connection;
        private readonly IModel _channel;

        public RabbitMqPublisher(IOptions<RabbitMqOptions> options)
        {
            _options = options.Value;
            var factory = new ConnectionFactory()
            {
                HostName = _options.HostName,
                UserName = _options.UserName,
                Password = _options.Password
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            // Declare exchange and queue
            _channel.ExchangeDeclare(exchange: _options.ExchangeName, type: ExchangeType.Topic, durable: true, autoDelete: false);
            _channel.QueueDeclare(queue: _options.QueueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
            _channel.QueueBind(queue: _options.QueueName, exchange: _options.ExchangeName, routingKey: _options.RoutingKey);
        }

        public Task PublishAsync<T>(T message)
        {
            var json = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(json);

            var props = _channel.CreateBasicProperties();
            props.DeliveryMode = 2; // persistent

            _channel.BasicPublish(exchange: _options.ExchangeName,
                                  routingKey: _options.RoutingKey,
                                  basicProperties: props,
                                  body: body);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            try
            {
                _channel?.Close();
                _channel?.Dispose();
            }
            catch { }
            try
            {
                _connection?.Close();
                _connection?.Dispose();
            }
            catch { }
        }
    }
}
