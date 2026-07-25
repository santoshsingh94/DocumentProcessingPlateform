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
        private readonly IOcrService _ocrService;
        private readonly IConfiguration _configuration;
        private IConnection? _connection;
        private IModel? _channel;

        public DocumentUploadedConsumer(
            IServiceProvider serviceProvider, 
            IOptions<RabbitMqOptions> options, 
            ILogger<DocumentUploadedConsumer> logger,
            IOcrService ocrService,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _options = options.Value;
            _ocrService = ocrService;
            _configuration = configuration;
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

                        // Resolve full file path
                        var fullFilePath = Path.IsPathRooted(document.StoragePath)
                            ? document.StoragePath
                            : Path.Combine(Directory.GetCurrentDirectory(), document.StoragePath);

                        if (!File.Exists(fullFilePath))
                        {
                            _logger.LogError("Document file not found at path: {Path} for document {DocumentId}", 
                                fullFilePath, @event.DocumentId);
                            document.Status = DocumentProcessing.Api.Models.Entities.DocumentStatus.Failed;
                            await db.SaveChangesAsync(stoppingToken);
                            _channel.BasicAck(ea.DeliveryTag, false);
                            return;
                        }

                        // Perform OCR extraction page by page
                        _logger.LogInformation("Starting OCR for document {DocumentId} at {Path}", 
                            @event.DocumentId, fullFilePath);

                        var pageTexts = await _ocrService.ExtractTextByPageAsync(fullFilePath, stoppingToken);
                        
                        if (pageTexts == null || pageTexts.Count == 0)
                        {
                            _logger.LogWarning(
                                "OCR returned no text for document {DocumentId}. " +
                                "Check Worker logs for errors. File: {Path}", 
                                @event.DocumentId, 
                                fullFilePath);
                        }
                        
                        // Save each page content to DocumentContent table
                        foreach (var (pageNumber, content) in pageTexts)
                        {
                            var documentContent = new DocumentProcessing.Api.Models.Entities.DocumentContent
                            {
                                Id = Guid.NewGuid(),
                                DocumentId = document.Id,
                                PageNumber = pageNumber,
                                Content = content,
                                CreatedAt = DateTime.UtcNow
                            };
                            
                            db.DocumentContents.Add(documentContent);
                        }

                        document.Status = DocumentProcessing.Api.Models.Entities.DocumentStatus.Completed;
                        document.UpdatedAt = DateTime.UtcNow;

                        _logger.LogInformation("OCR completed for document {DocumentId}. Processed {PageCount} page(s)", 
                            @event.DocumentId, pageTexts.Count);

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
