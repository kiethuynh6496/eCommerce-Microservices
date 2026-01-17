namespace Product.Application.DTOs;

public record ProductDto(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    int Stock,
    string? ImageUrl,
    string Category,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
