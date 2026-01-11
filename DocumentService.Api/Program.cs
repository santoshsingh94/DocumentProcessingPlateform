using DocumentProcessing.Api.Models;
using DocumentProcessing.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.StackExchangeRedis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register DBContext
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register distributed cache (Redis). Ensure you have a "Redis" connection string in configuration.
// For local/dev you can still use AddDistributedMemoryCache(). In production prefer Redis.
var redisConnection = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrEmpty(redisConnection))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnection;
        options.InstanceName = builder.Configuration.GetValue<string>("Redis:InstanceName") ?? "docsvc:";
    });
}
else
{
    // Fallback to in-memory distributed cache when Redis connection string is not provided
    builder.Services.AddDistributedMemoryCache();
}

builder.Services.AddScoped<IDocumentService, DocumentService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
