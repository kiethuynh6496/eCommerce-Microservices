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
using MassTransit;
using Order.Application.Sagas;
using MassTransit.EntityFrameworkCoreIntegration;

var builder = WebApplication.CreateBuilder(args);

// Configure additional configuration sources for Docker
builder.Configuration
    .AddEnvironmentVariables();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ============================================
// MongoDB Configuration (Domain Data)
// ============================================
var mongoConnectionString = builder.Configuration.GetConnectionString("MongoDb");
var databaseName = builder.Configuration.GetSection("MongoDb:DatabaseName").Value;
builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseMongoDB(mongoConnectionString!, databaseName!));

// ============================================
// MySQL Configuration (Saga State Persistence)
// ============================================
var mysqlConnectionString = builder.Configuration.GetConnectionString("SagaDb")
    ?? "Server=localhost;Database=order_saga;User=root;Password=root;";

// Register Saga DbContext
builder.Services.AddDbContext<SagaDbContext>(options =>
    options.UseMySql(
        mysqlConnectionString,
        ServerVersion.AutoDetect(mysqlConnectionString),
        mysqlOptions =>
        {
            mysqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
            mysqlOptions.MigrationsAssembly("Order.Infrastructure");
        }));

// ============================================
// Register Repositories
// ============================================
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IOrderItemRepository, OrderItemRepository>();

// ============================================
// Register Services
// ============================================
builder.Services.AddScoped<IOrderService, OrderAppService>();

// ============================================
// Register Validators
// ============================================
builder.Services.AddScoped<IValidator<CreateOrderDto>, CreateOrderDtoValidator>();
builder.Services.AddScoped<IValidator<UpdateOrderDto>, UpdateOrderDtoValidator>();
builder.Services.AddScoped<IValidator<UpdateOrderStatusDto>, UpdateOrderStatusDtoValidator>();

// ============================================
// HttpClient + Polly
// ============================================
builder.Services.AddHttpClient<IProductServiceClient, ProductServiceClient>(client =>
{
    var gatewayUrl = builder.Configuration["ExternalServices:Gateway:BaseUrl"] ?? "http://gateway";
    client.BaseAddress = new Uri($"{gatewayUrl}/products/");
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.DefaultRequestHeaders.Add("User-Agent", "OrderService/1.0");
})
.AddPolicyHandler(GetRetryPolicy())
.AddPolicyHandler(GetCircuitBreakerPolicy())
.AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(10)));

// ============================================
// MassTransit Configuration with Saga
// ============================================
builder.Services.AddMassTransit(x =>
{
    // Add Saga State Machine
    x.AddSagaStateMachine<OrderStateMachine, OrderState>()
        .EntityFrameworkRepository(r =>
        {
            r.ExistingDbContext<SagaDbContext>();
            r.UseMySql();
        });

    // Configure RabbitMQ
    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitMqHost = builder.Configuration["RabbitMq:Host"] ?? "localhost";
        var rabbitMqUser = builder.Configuration["RabbitMq:Username"] ?? "guest";
        var rabbitMqPass = builder.Configuration["RabbitMq:Password"] ?? "guest";
        var rabbitMqVHost = builder.Configuration["RabbitMq:VirtualHost"] ?? "/";

        cfg.Host(rabbitMqHost, rabbitMqVHost, h =>
        {
            h.Username(rabbitMqUser);
            h.Password(rabbitMqPass);
        });

        // Message Retry Configuration
        cfg.UseMessageRetry(retry =>
        {
            retry.Incremental(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
        });

        // Configure Saga Endpoint
        cfg.ReceiveEndpoint("order-saga-queue", e =>
        {
            e.UseMessageRetry(r => r.Intervals(100, 500, 1000, 2000, 5000));

            e.ConfigureSaga<OrderState>(context);
        });

        // Auto configure endpoints for all consumers
        cfg.ConfigureEndpoints(context);
    });
});

// ============================================
// CORS
// ============================================
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

// ============================================
// Configure the HTTP request pipeline
// ============================================
if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName == "Docker")
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

// ============================================
// Health check endpoint
// ============================================
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    service = "order",
    timestamp = DateTime.UtcNow,
    environment = app.Environment.EnvironmentName
}));

// ============================================
// Database Initialization
// ============================================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var isDocker = app.Environment.EnvironmentName == "Docker";
    var maxRetries = isDocker ? 5 : 1;
    var retryDelay = 5000;

    // Initialize MongoDB (Domain Data)
    Console.WriteLine("=== Initializing MongoDB ===");
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            var mongoContext = services.GetRequiredService<OrderDbContext>();
            await mongoContext.Database.CanConnectAsync();
            Console.WriteLine("✓ MongoDB connection successful");
            break;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ MongoDB attempt {i + 1}/{maxRetries} failed: {ex.Message}");
            if (i < maxRetries - 1)
            {
                Console.WriteLine($"  Retrying in {retryDelay / 1000}s...");
                await Task.Delay(retryDelay);
            }
            else
            {
                Console.WriteLine("⚠ MongoDB connection failed - app will retry on requests");
            }
        }
    }

    // Initialize MySQL (Saga State)
    Console.WriteLine("=== Initializing MySQL for Saga ===");
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            var sagaContext = services.GetRequiredService<SagaDbContext>();

            // Auto-migrate in Docker environment
            if (isDocker)
            {
                Console.WriteLine("Running Saga database migrations...");
                await sagaContext.Database.MigrateAsync();
            }
            else
            {
                // Just test connection in dev
                await sagaContext.Database.CanConnectAsync();
            }

            Console.WriteLine("✓ MySQL Saga database ready");
            break;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ MySQL attempt {i + 1}/{maxRetries} failed: {ex.Message}");
            if (i < maxRetries - 1)
            {
                Console.WriteLine($"  Retrying in {retryDelay / 1000}s...");
                await Task.Delay(retryDelay);
            }
            else
            {
                Console.WriteLine("⚠ MySQL connection failed - Saga functionality may be impaired");
            }
        }
    }
}

Console.WriteLine("=== Order Service Started ===");
app.Run();

// ============================================
// Polly Policies
// ============================================

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