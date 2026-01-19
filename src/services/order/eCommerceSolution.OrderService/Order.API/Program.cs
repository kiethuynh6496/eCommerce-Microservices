using Microsoft.EntityFrameworkCore;
using FluentValidation;
using Order.Application.Services;
using Order.Application.Validators;
using Order.Infrastructure.Data;
using Order.Infrastructure.Repositories;
using Order.Application.DTOs;
using Order.Domain.Repositories;
using Order.Application.Interfaces.External;
using Order.Infrastructure.ExternalServices;
using Polly;
using Polly.Extensions.Http;

var builder = WebApplication.CreateBuilder(args);

// Configure additional configuration sources for Docker
builder.Configuration
    .AddEnvironmentVariables();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// MongoDB Configuration
var connectionString = builder.Configuration.GetConnectionString("MongoDb");
var databaseName = builder.Configuration.GetSection("MongoDb:DatabaseName").Value;
builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseMongoDB(connectionString!, databaseName!));

// Register Repositories
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IOrderItemRepository, OrderItemRepository>();

// Register Services
builder.Services.AddScoped<IOrderService, OrderAppService>();

// Register Validators
builder.Services.AddScoped<IValidator<CreateOrderDto>, CreateOrderDtoValidator>();
builder.Services.AddScoped<IValidator<UpdateOrderDto>, UpdateOrderDtoValidator>();
builder.Services.AddScoped<IValidator<UpdateOrderStatusDto>, UpdateOrderStatusDtoValidator>();

// HttpClient + Polly
builder.Services.AddHttpClient<IProductServiceClient, ProductServiceClient>(client =>
{
    // Gọi qua Gateway
    var gatewayUrl = builder.Configuration["ExternalServices:Gateway:BaseUrl"] ?? "http://gateway";
    client.BaseAddress = new Uri($"{gatewayUrl}/products/");

    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.DefaultRequestHeaders.Add("User-Agent", "OrderService/1.0");
})
.AddPolicyHandler(GetRetryPolicy())
.AddPolicyHandler(GetCircuitBreakerPolicy())
.AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(10)));

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName == "Docker")
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

// Health check endpoint for Docker
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    service = "order",
    timestamp = DateTime.UtcNow,
    environment = app.Environment.EnvironmentName
}));

// Initialize MongoDB connection with retry logic for Docker
using (var scope = app.Services.CreateScope())
{
    var maxRetries = app.Environment.EnvironmentName == "Docker" ? 3 : 1;
    var retryDelay = 5000; // 5 seconds
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            // Test MongoDB connection by getting database
            var database = dbContext.Database;
            await database.CanConnectAsync();
            Console.WriteLine("MongoDB connection successful");
            break;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"MongoDB connection attempt {i + 1}/{maxRetries} failed: {ex.Message}");
            if (i < maxRetries - 1)
            {
                Console.WriteLine($"Retrying MongoDB connection in {retryDelay / 1000} seconds...");
                await Task.Delay(retryDelay);
            }
            else
            {
                Console.WriteLine("MongoDB connection failed after all retries");
                // Don't throw - let the app start and retry on requests
            }
        }
    }
}

app.Run();

/// <summary>
/// Retry Policy: Retry 3 lần với exponential backoff
/// </summary>
static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.NotFound)
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (outcome, timespan, retryCount, context) =>
            {
                var statusCode = outcome.Result?.StatusCode.ToString() ?? "Exception";
                Console.WriteLine($"[Retry Policy] Attempt {retryCount} after {timespan.TotalSeconds}s delay. Status: {statusCode}");
            });
}

/// <summary>
/// Circuit Breaker Policy: Break sau 5 lỗi liên tiếp, nghỉ 30 giây
/// </summary>
static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: 5,
            durationOfBreak: TimeSpan.FromSeconds(30),
            onBreak: (outcome, timespan) =>
            {
                var statusCode = outcome.Result?.StatusCode.ToString() ?? outcome.Exception?.Message;
                Console.WriteLine($"[Circuit Breaker] Circuit opened for {timespan.TotalSeconds}s. Reason: {statusCode}");
            },
            onReset: () =>
            {
                Console.WriteLine("[Circuit Breaker] Circuit reset - back to normal operation");
            },
            onHalfOpen: () =>
            {
                Console.WriteLine("[Circuit Breaker] Circuit is half-open - testing connection");
            });
}