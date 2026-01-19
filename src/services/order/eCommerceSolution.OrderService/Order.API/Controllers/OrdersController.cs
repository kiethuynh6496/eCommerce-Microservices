using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using Order.Application.DTOs;
using Order.Application.Services;

namespace Order.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly IValidator<CreateOrderDto> _createOrderValidator;
    private readonly IValidator<UpdateOrderDto> _updateOrderValidator;
    private readonly IValidator<UpdateOrderStatusDto> _updateStatusValidator;

    public OrdersController(
        IOrderService orderService,
        IValidator<CreateOrderDto> createOrderValidator,
        IValidator<UpdateOrderDto> updateOrderValidator,
        IValidator<UpdateOrderStatusDto> updateStatusValidator)
    {
        _orderService = orderService;
        _createOrderValidator = createOrderValidator;
        _updateOrderValidator = updateOrderValidator;
        _updateStatusValidator = updateStatusValidator;
    }


    [HttpGet("{id}")]
    public async Task<IActionResult> GetOrderById(string id)
    {
        var order = await _orderService.GetOrderByIdAsync(id);
        if (order == null)
        {
            return NotFound(new { message = "Order not found" });
        }
        return Ok(order);
    }

    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetOrdersByUserId(string userId)
    {
        var orders = await _orderService.GetOrdersByUserIdAsync(userId);
        return Ok(orders);
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDto createOrderDto)
    {
        var validationResult = await _createOrderValidator.ValidateAsync(createOrderDto);
        if (!validationResult.IsValid)
        {
            return BadRequest(new { errors = validationResult.Errors.Select(e => e.ErrorMessage) });
        }

        var order = await _orderService.CreateOrderAsync(createOrderDto);
        return CreatedAtAction(nameof(GetOrderById), new { id = order.Id }, order);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateOrder(string id, [FromBody] UpdateOrderDto updateOrderDto)
    {
        var validationResult = await _updateOrderValidator.ValidateAsync(updateOrderDto);
        if (!validationResult.IsValid)
        {
            return BadRequest(new { errors = validationResult.Errors.Select(e => e.ErrorMessage) });
        }

        var success = await _orderService.UpdateOrderAsync(id, updateOrderDto);
        if (!success)
        {
            return NotFound(new { message = "Order not found or cannot be updated" });
        }

        return NoContent();
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateOrderStatus(string id, [FromBody] UpdateOrderStatusDto updateStatusDto)
    {
        var validationResult = await _updateStatusValidator.ValidateAsync(updateStatusDto);
        if (!validationResult.IsValid)
        {
            return BadRequest(new { errors = validationResult.Errors.Select(e => e.ErrorMessage) });
        }

        var success = await _orderService.UpdateOrderStatusAsync(id, updateStatusDto);
        if (!success)
        {
            return NotFound(new { message = "Order not found" });
        }

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteOrder(string id)
    {
        var success = await _orderService.DeleteOrderAsync(id);
        if (!success)
        {
            return NotFound(new { message = "Order not found" });
        }

        return NoContent();
    }
}