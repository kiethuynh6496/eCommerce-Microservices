using Identity.API.Extensions;
using Identity.API.Middleware;
using Identity.Application;
using Identity.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure additional configuration sources
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.Secrets.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.Secrets.json", optional: true, reloadOnChange: true)
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
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Identity Service API V1");
        c.RoutePrefix = string.Empty; // Set Swagger UI at root
    });
}

// Enable Swagger for Docker environment
if (app.Environment.EnvironmentName == "Docker")
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Identity Service API V1");
        c.RoutePrefix = string.Empty;
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

// Initialize database
using (var scope = app.Services.CreateScope())
{
    try
    {
        await Identity.Infrastructure.Persistence.DbInitializer.InitializeAsync(scope.ServiceProvider);
        Log.Information("Database initialized successfully");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "An error occurred while initializing the database");

        // Trong môi trường Docker, retry nếu database chưa sẵn sàng
        if (app.Environment.EnvironmentName == "Docker")
        {
            Log.Warning("Retrying database initialization in 5 seconds...");
            await Task.Delay(5000);

            try
            {
                await Identity.Infrastructure.Persistence.DbInitializer.InitializeAsync(scope.ServiceProvider);
                Log.Information("Database initialized successfully on retry");
            }
            catch (Exception retryEx)
            {
                Log.Error(retryEx, "Database initialization failed after retry");
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