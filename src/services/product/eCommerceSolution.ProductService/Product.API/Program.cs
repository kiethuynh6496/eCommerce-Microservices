using Product.API.Endpoints;
using Product.Application;
using Product.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Configure additional configuration sources
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// Add services to the container
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

// Add Application and Infrastructure layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName == "Docker")
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();

// Health check endpoint for Docker
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    service = "product",
    timestamp = DateTime.UtcNow,
    environment = app.Environment.EnvironmentName
}));

app.MapProductEndpoints();

// Initialize database with retry logic for Docker
using (var scope = app.Services.CreateScope())
{
    var serviceProvider = scope.ServiceProvider;
    var maxRetries = app.Environment.EnvironmentName == "Docker" ? 3 : 1;
    var retryDelay = 5000; // 5 seconds

    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            // Apply migrations if exists
            var dbContext = serviceProvider.GetRequiredService<Product.Infrastructure.Data.ApplicationDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
            // Or use: await dbContext.Database.MigrateAsync();

            Console.WriteLine("Database initialized successfully");
            break;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Database initialization attempt {i + 1} failed: {ex.Message}");

            if (i < maxRetries - 1)
            {
                Console.WriteLine($"Retrying in {retryDelay / 1000} seconds...");
                await Task.Delay(retryDelay);
            }
            else
            {
                Console.WriteLine("Database initialization failed after all retries");
                // Don't throw in production, let the app start and retry on requests
            }
        }
    }
}

app.Run();