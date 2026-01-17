using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Product.Application.DTOs;
using Product.Application.Services;
using Product.Application.Validators;

namespace Product.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddScoped<IValidator<CreateProductDto>, CreateProductValidator>();
            services.AddScoped<IValidator<UpdateProductDto>, UpdateProductValidator>();
            services.AddScoped<IProductService, ProductService>();

            return services;
        }
    }
}
