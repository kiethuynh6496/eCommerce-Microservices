namespace Order.Application.DTOs;

public class UpdateOrderDto
{
    public string ShippingAddress { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Notes { get; set; }
}