using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Product.Application.DTOs;
using Product.Application.Services;

namespace Product.API.Endpoints
{
    public static class ProductEndpoints
    {
        public static void MapProductEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/products")
                .WithTags("Products");

            // GET: /api/products
            group.MapGet("/", GetAllProducts)
                .WithName("GetAllProducts")
                .Produces<List<ProductDto>>(StatusCodes.Status200OK);

            // GET: /api/products/{id}
            group.MapGet("/{id:guid}", GetProductById)
                .WithName("GetProductById")
                .Produces<ProductDto>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status404NotFound);

            // POST: /api/products
            group.MapPost("/", CreateProduct)
                .WithName("CreateProduct")
                .Produces<ProductDto>(StatusCodes.Status201Created)
                .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

            // PUT: /api/products/{id}
            group.MapPut("/{id:guid}", UpdateProduct)
                .WithName("UpdateProduct")
                .Produces<ProductDto>(StatusCodes.Status200OK)
                .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status404NotFound);

        }

        private static async Task<IResult> GetAllProducts(
            IProductService productService,
            CancellationToken cancellationToken)
        {
            var products = await productService.GetAllAsync(cancellationToken);
            return Results.Ok(products);
        }

        private static async Task<IResult> GetProductById(
            Guid id,
            IProductService productService,
            CancellationToken cancellationToken)
        {
            var product = await productService.GetByIdAsync(id, cancellationToken);
            return product == null ? Results.NotFound() : Results.Ok(product);
        }

        private static async Task<IResult> CreateProduct(
            CreateProductDto dto,
            IProductService productService,
            CancellationToken cancellationToken)
        {
            try
            {
                var product = await productService.CreateAsync(dto, cancellationToken);
                return Results.CreatedAtRoute("GetProductById", new { id = product.Id }, product);
            }
            catch (ValidationException ex)
            {
                return Results.BadRequest(new ProblemDetails
                {
                    Title = "Validation Error",
                    Detail = ex.Message,
                    Status = StatusCodes.Status400BadRequest
                });
            }
        }

        private static async Task<IResult> UpdateProduct(
            Guid id,
            UpdateProductDto dto,
            IProductService productService,
            CancellationToken cancellationToken)
        {
            try
            {
                var product = await productService.UpdateAsync(id, dto, cancellationToken);
                return Results.Ok(product);
            }
            catch (ValidationException ex)
            {
                return Results.BadRequest(new ProblemDetails
                {
                    Title = "Validation Error",
                    Detail = ex.Message,
                    Status = StatusCodes.Status400BadRequest
                });
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        }
    }
}
