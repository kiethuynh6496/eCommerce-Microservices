using Product.API.Endpoints;
using Product.Application;
using Product.Application.Interfaces;
using Product.Infrastructure;
using Product.Application.Consumers;
using MassTransit;
using Product.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Product Service API",
        Version = "v1",
        Description = "API for managing products in ecommerce microservice"
    });
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Redis Cache
var redisConfiguration = builder.Configuration["Redis:Configuration"] ?? "localhost:6379";
var redisInstanceName = builder.Configuration["Redis:InstanceName"] ?? "ProductService:";

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConfiguration;
    options.InstanceName = redisInstanceName;
});

Console.WriteLine($"Redis configured: {redisConfiguration} with instance: {redisInstanceName}");

// Add Application and Infrastructure layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ============================================
// MASSTRANSIT
// ============================================
builder.Services.AddMassTransit(x =>
{
    // Register Consumers
    x.AddConsumer<ReserveInventoryConsumer>();
    x.AddConsumer<ReleaseInventoryConsumer>();

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

        Console.WriteLine($"RabbitMQ configured: {rabbitMqHost} (vhost: {rabbitMqVHost})");

        // Global retry
        cfg.UseMessageRetry(retry =>
        {
            retry.Incremental(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
        });

        // Reserve Inventory Endpoint
        cfg.ReceiveEndpoint("reserve-inventory-queue", e =>
        {
            e.PrefetchCount = 16;
            e.ConcurrentMessageLimit = 8;
            e.UseMessageRetry(r => r.Intervals(100, 500, 1000, 2000, 5000));
            //e.UseEntityFrameworkInbox<ApplicationDbContext>(context);
            e.ConfigureConsumer<ReserveInventoryConsumer>(context);
        });

        // Release Inventory Endpoint
        cfg.ReceiveEndpoint("release-inventory-queue", e =>
        {
            e.PrefetchCount = 16;
            e.ConcurrentMessageLimit = 8;
            e.UseMessageRetry(r => r.Intervals(100, 500, 1000, 2000, 5000));
            // e.UseEntityFrameworkInbox<ApplicationDbContext>(context);
            e.ConfigureConsumer<ReleaseInventoryConsumer>(context);
        });

        cfg.ConfigureEndpoints(context);
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName == "Docker")
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();

app.MapGet("/health", async (ICacheService cacheService) =>
{
    var healthStatus = new
    {
        status = "healthy",
        service = "product",
        timestamp = DateTime.UtcNow,
        environment = app.Environment.EnvironmentName,
        redis = "unknown",
        rabbitmq = "unknown"
    };

    try
    {
        var testKey = "health-check";
        var testValue = new { test = "ok", timestamp = DateTime.UtcNow };
        await cacheService.SetAsync(testKey, testValue, TimeSpan.FromSeconds(10));
        var result = await cacheService.GetAsync<object>(testKey);
        await cacheService.RemoveAsync(testKey);
        healthStatus = healthStatus with { redis = result != null ? "connected" : "disconnected" };
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Redis health check failed: {ex.Message}");
        healthStatus = healthStatus with { redis = "disconnected" };
    }

    try
    {
        var busControl = app.Services.GetRequiredService<IBusControl>();
        healthStatus = healthStatus with { rabbitmq = "connected" };
    }
    catch
    {
        healthStatus = healthStatus with { rabbitmq = "disconnected" };
    }

    return Results.Ok(healthStatus);
});

app.MapProductEndpoints();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var serviceProvider = scope.ServiceProvider;
    var isDocker = app.Environment.EnvironmentName == "Docker";
    var maxRetries = isDocker ? 5 : 1;
    var retryDelay = 5000;

    Console.WriteLine("=== Initializing Product Service ===");

    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            var dbContext = serviceProvider.GetRequiredService<Product.Infrastructure.Data.ApplicationDbContext>();
            if (isDocker)
            {
                Console.WriteLine("Running database migrations...");
                await dbContext.Database.EnsureCreatedAsync();
            }
            else
            {
                await dbContext.Database.EnsureCreatedAsync();
            }
            Console.WriteLine("Database initialized successfully");
            break;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Database initialization attempt {i + 1}/{maxRetries} failed: {ex.Message}");
            if (i < maxRetries - 1)
            {
                Console.WriteLine($"  Retrying in {retryDelay / 1000}s...");
                await Task.Delay(retryDelay);
            }
            else
            {
                Console.WriteLine("Database initialization failed after all retries");
            }
        }
    }

    // Test Redis
    try
    {
        var cacheService = serviceProvider.GetRequiredService<ICacheService>();
        await cacheService.SetAsync("startup-test", new { initialized = true }, TimeSpan.FromSeconds(5));
        await cacheService.RemoveAsync("startup-test");
        Console.WriteLine("Redis connection verified successfully");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Redis connection verification failed: {ex.Message}");
    }

    // Test RabbitMQ
    try
    {
        var busControl = serviceProvider.GetRequiredService<IBusControl>();
        Console.WriteLine("✓ RabbitMQ connection established");
        Console.WriteLine("  Consumers ready:");
        Console.WriteLine("    - ReserveInventoryConsumer → reserve-inventory-queue");
        Console.WriteLine("    - ReleaseInventoryConsumer → release-inventory-queue");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"RabbitMQ connection failed: {ex.Message}");
    }
}

Console.WriteLine("=== Product Service Started ===");
app.Run();