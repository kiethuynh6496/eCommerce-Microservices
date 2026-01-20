using FluentValidation;
using Product.Application.DTOs;
using Product.Application.Interfaces;
using Product.Domain.Repositories;

namespace Product.Application.Services
{
    public class ProductService : IProductService
    {
        private readonly IProductRepository _productRepository;
        private readonly IValidator<CreateProductDto> _createValidator;
        private readonly IValidator<UpdateProductDto> _updateValidator;
        private readonly ICacheService _cacheService;

        // Cache key patterns
        private const string CACHE_KEY_SINGLE = "product:{0}";
        private const string CACHE_KEY_ALL = "product:all";

        // Cache TTL
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

        public ProductService(
            IProductRepository productRepository,
            IValidator<CreateProductDto> createValidator,
            IValidator<UpdateProductDto> updateValidator,
            ICacheService cacheService)
        {
            _productRepository = productRepository;
            _createValidator = createValidator;
            _updateValidator = updateValidator;
            _cacheService = cacheService;
        }

        public async Task<ProductDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            var cacheKey = string.Format(CACHE_KEY_SINGLE, id);

            // Try to get from cache first
            var cachedProduct = await _cacheService.GetAsync<ProductDto>(cacheKey);
            if (cachedProduct != null)
            {
                return cachedProduct;
            }

            // Cache miss - get from database
            var product = await _productRepository.GetByIdAsync(id, cancellationToken);
            if (product == null)
            {
                return null;
            }

            var productDto = MapToDto(product);

            // Store in cache
            await _cacheService.SetAsync(cacheKey, productDto, _cacheDuration);

            return productDto;
        }

        public async Task<List<ProductDto>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            // Try to get from cache
            var cachedProducts = await _cacheService.GetAsync<List<ProductDto>>(CACHE_KEY_ALL);
            if (cachedProducts != null)
            {
                return cachedProducts;
            }

            // Cache miss - get from database
            var products = await _productRepository.GetAllAsync(cancellationToken);
            var productDtos = products.Select(MapToDto).ToList();

            // Store in cache
            await _cacheService.SetAsync(CACHE_KEY_ALL, productDtos, _cacheDuration);

            return productDtos;
        }

        public async Task<ProductDto> CreateAsync(CreateProductDto dto, CancellationToken cancellationToken = default)
        {
            // Validate input
            var validationResult = await _createValidator.ValidateAsync(dto, cancellationToken);
            if (!validationResult.IsValid)
            {
                var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
                throw new ValidationException(errors);
            }

            var product = new Product.Domain.Entities.Product
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                Description = dto.Description,
                Price = dto.Price,
                Stock = dto.Stock,
                ImageUrl = dto.ImageUrl,
                Category = dto.Category,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var created = await _productRepository.AddAsync(product, cancellationToken);

            // Invalidate "all products" cache
            await _cacheService.RemoveAsync(CACHE_KEY_ALL);

            return MapToDto(created);
        }

        public async Task<ProductDto> UpdateAsync(string id, UpdateProductDto dto, CancellationToken cancellationToken = default)
        {
            // Validate input
            var validationResult = await _updateValidator.ValidateAsync(dto, cancellationToken);
            if (!validationResult.IsValid)
            {
                var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
                throw new ValidationException(errors);
            }

            var product = await _productRepository.GetByIdAsync(id, cancellationToken);
            if (product == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy sản phẩm với ID: {id}");
            }

            // Update properties
            product.Name = dto.Name;
            product.Description = dto.Description;
            product.Price = dto.Price;
            product.Stock = dto.Stock;
            product.ImageUrl = dto.ImageUrl;
            product.Category = dto.Category;
            product.IsActive = dto.IsActive;
            product.UpdatedAt = DateTime.UtcNow;

            await _productRepository.UpdateAsync(product, cancellationToken);

            // Invalidate caches
            var cacheKey = string.Format(CACHE_KEY_SINGLE, id);
            await _cacheService.RemoveAsync(cacheKey);
            await _cacheService.RemoveAsync(CACHE_KEY_ALL);

            return MapToDto(product);
        }

        private static ProductDto MapToDto(Product.Domain.Entities.Product product)
        {
            return new ProductDto(
                product.Id,
                product.Name,
                product.Description,
                product.Price,
                product.Stock,
                product.ImageUrl,
                product.Category,
                product.IsActive,
                product.CreatedAt,
                product.UpdatedAt
            );
        }
    }
}