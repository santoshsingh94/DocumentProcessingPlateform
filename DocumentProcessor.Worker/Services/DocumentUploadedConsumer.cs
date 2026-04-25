using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using DocumentProcessing.Shared.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DocumentProcessor.Worker.Configuration;
using DocumentProcessing.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DocumentProcessor.Worker.Services
{
    public class DocumentUploadedConsumer : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DocumentUploadedConsumer> _logger;
        private readonly RabbitMqOptions _options;
        private IConnection? _connection;
        private IModel? _channel;

        public DocumentUploadedConsumer(IServiceProvider serviceProvider, IOptions<RabbitMqOptions> options, ILogger<DocumentUploadedConsumer> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _options = options.Value;
            InitializeRabbitMqListener();
        }

        private void InitializeRabbitMqListener()
        {
            var factory = new ConnectionFactory()
            {
                HostName = _options.HostName,
                UserName = _options.UserName,
                Password = _options.Password,
                DispatchConsumersAsync = true
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            _channel.ExchangeDeclare(exchange: _options.ExchangeName, type: ExchangeType.Topic, durable: true, autoDelete: false);
            _channel.QueueDeclare(queue: _options.QueueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
            _channel.QueueBind(queue: _options.QueueName, exchange: _options.ExchangeName, routingKey: _options.RoutingKey);

            _channel.BasicQos(0, 1, false);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_channel == null) return;

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    var @event = System.Text.Json.JsonSerializer.Deserialize<DocumentUploadedEvent>(message);
                    if (@event == null)
                    {
                        _logger.LogWarning("Received null or invalid DocumentUploadedEvent");
                        _channel.BasicAck(ea.DeliveryTag, false);
                        return;
                    }

                    // Use scoped DbContext for EF Core operations
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                        // Update status to Processing
                        var document = await db.Documents.FirstOrDefaultAsync(d => d.Id == @event.DocumentId);
                        if (document == null)
                        {
                            _logger.LogWarning("Document {DocumentId} not found", @event.DocumentId);
                            _channel.BasicAck(ea.DeliveryTag, false);
                            return;
                        }

                        document.Status = DocumentProcessing.Api.Models.Entities.DocumentStatus.Processing;
                        document.UpdatedAt = DateTime.UtcNow;
                        await db.SaveChangesAsync(stoppingToken);

                        // Simulate processing
                        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

                        document.Status = DocumentProcessing.Api.Models.Entities.DocumentStatus.Completed;
                        document.UpdatedAt = DateTime.UtcNow;
                        await db.SaveChangesAsync(stoppingToken);
                    }

                    // Ack on success
                    _channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing DocumentUploadedEvent");
                    try
                    {
                        // Nack without requeue
                        _channel.BasicNack(ea.DeliveryTag, false, false);
                    }
                    catch (Exception nackEx)
                    {
                        _logger.LogError(nackEx, "Failed to Nack message");
                    }
                }
            };

            _channel.BasicConsume(queue: _options.QueueName, autoAck: false, consumer: consumer);

            // Keep running until stopped
            await Task.CompletedTask;
        }

        public override void Dispose()
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
            base.Dispose();
        }
    }
}
