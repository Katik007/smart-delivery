using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartDelivery.CatalogService.Application.DTOs;
using SmartDelivery.Shared.Security;

namespace SmartDelivery.CatalogService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CatalogController : ControllerBase
{
    private readonly ICatalogService _catalogService;

    public CatalogController(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    [HttpGet("products")]
    [ProducesResponseType(typeof(IReadOnlyList<ProductDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProducts([FromQuery] Guid? categoryId, CancellationToken ct)
    {
        var result = await _catalogService.GetProductsAsync(categoryId, ct);
        return Ok(result.Value);
    }

    [HttpGet("products/{id:guid}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProductById(Guid id, CancellationToken ct)
    {
        var result = await _catalogService.GetProductByIdAsync(id, ct);
        if (result.IsFailure) return NotFound(new { error = result.Error });
        return Ok(result.Value);
    }

    [HttpPost("products")]
    [Authorize(Roles = $"{Roles.Seller},{Roles.Admin}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequest request, CancellationToken ct)
    {
        var sellerIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(sellerIdClaim, out var sellerId))
            return Unauthorized();

        var result = await _catalogService.CreateProductAsync(request, sellerId, ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });

        return CreatedAtAction(nameof(GetProductById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("products/{id:guid}")]
    [Authorize(Roles = $"{Roles.Seller},{Roles.Admin}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] UpdateProductRequest request, CancellationToken ct)
    {
        var sellerIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(sellerIdClaim, out var sellerId))
            return Unauthorized();

        var result = await _catalogService.UpdateProductAsync(id, request, sellerId, ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return Ok(result.Value);
    }

    [HttpPost("products/{id:guid}/restock")]
    [Authorize(Roles = $"{Roles.Seller},{Roles.Admin}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Restock(Guid id, [FromBody] RestockRequest request, CancellationToken ct)
    {
        var result = await _catalogService.RestockProductAsync(id, request.Quantity, ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return Ok(new { success = true });
    }

    [HttpGet("categories")]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategories(CancellationToken ct)
    {
        var result = await _catalogService.GetCategoriesAsync(ct);
        return Ok(result.Value);
    }

    [HttpPost("categories")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryRequest request, CancellationToken ct)
    {
        var result = await _catalogService.CreateCategoryAsync(request, ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error });
        return Ok(result.Value);
    }
}
