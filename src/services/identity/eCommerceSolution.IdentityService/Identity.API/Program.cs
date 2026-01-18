using Identity.API.Extensions;
using Identity.API.Middleware;
using Identity.Application;
using Identity.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure additional configuration sources for Docker
builder.Configuration
    .AddEnvironmentVariables();

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add custom services
builder.Services.AddSwaggerDocumentation();
builder.Services.AddCorsPolicy();

// Add Application and Infrastructure layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName == "Docker")
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Identity Service API V1");
        c.RoutePrefix = string.Empty; // Set Swagger UI at root
    });
}

// Use custom middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Health check endpoint for Docker
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    service = "identity",
    timestamp = DateTime.UtcNow,
    environment = app.Environment.EnvironmentName
})).AllowAnonymous();

// Initialize database with retry logic for Docker
using (var scope = app.Services.CreateScope())
{
    var maxRetries = app.Environment.EnvironmentName == "Docker" ? 3 : 1;
    var retryDelay = 5000; // 5 seconds

    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            await Identity.Infrastructure.Persistence.DbInitializer.InitializeAsync(scope.ServiceProvider);
            Log.Information("Database initialized successfully");
            break;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while initializing the database (Attempt {Attempt}/{MaxRetries})", i + 1, maxRetries);

            if (i < maxRetries - 1)
            {
                Log.Warning("Retrying database initialization in {Delay} seconds...", retryDelay / 1000);
                await Task.Delay(retryDelay);
            }
            else
            {
                Log.Error("Database initialization failed after all retries");
            }
        }
    }
}

try
{
    Log.Information("Starting Identity Service API on environment: {Environment}", app.Environment.EnvironmentName);
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Identity Service API terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}