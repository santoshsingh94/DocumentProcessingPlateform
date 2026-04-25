using DocumentProcessor.Worker;
using DocumentProcessor.Worker.Configuration;
using DocumentProcessor.Worker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Bind RabbitMQ options from configuration
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));

// Register DbContext from API project - reuse same connection string
builder.Services.AddDbContext<DocumentProcessing.Api.Models.ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));


builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<DocumentUploadedConsumer>();

var host = builder.Build();
host.Run();
