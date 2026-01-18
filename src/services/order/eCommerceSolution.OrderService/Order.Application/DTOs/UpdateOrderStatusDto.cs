using Order.Domain.Entities;

namespace Order.Application.DTOs;

public class UpdateOrderStatusDto
{
    public OrderStatus Status { get; set; }
}