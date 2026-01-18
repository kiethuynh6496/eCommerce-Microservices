using Microsoft.EntityFrameworkCore;
using FluentValidation;
using OrderService.Application.Services;
using OrderService.Application.Validators;
using OrderService.Infrastructure.Data;
using OrderService.Infrastructure.Repositories;
using Order.Application.DTOs;
using Order.Domain.Repositories;

var builder = WebApplication.CreateBuilder(args);

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
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

app.Run();