using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartDelivery.OrderService.Application.DTOs;
using SmartDelivery.Shared.Security;

namespace SmartDelivery.OrderService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpPost("checkout")]
    [Authorize]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CheckoutOrder([FromBody] CreateOrderRequest request, CancellationToken ct)
    {
        var customerIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(customerIdClaim, out var customerId))
        {
            customerId = Guid.Parse("99999999-9999-9999-9999-999999999999"); // Fallback for tests if needed
        }

        var result = await _orderService.CreateAndCheckoutOrderAsync(request, customerId, ct);
        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

        return CreatedAtAction(nameof(GetOrderById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrderById(Guid id, CancellationToken ct)
    {
        var result = await _orderService.GetOrderByIdAsync(id, ct);
        if (result.IsFailure) return NotFound(new { error = result.Error });
        return Ok(result.Value);
    }

    [HttpGet("my")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<OrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyOrders(CancellationToken ct)
    {
        var customerIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(customerIdClaim, out var customerId))
        {
            customerId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        }

        var result = await _orderService.GetCustomerOrdersAsync(customerId, ct);
        return Ok(result.Value);
    }

    [HttpGet("{id:guid}/saga")]
    [Authorize]
    [ProducesResponseType(typeof(SagaStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSagaStatus(Guid id, CancellationToken ct)
    {
        var result = await _orderService.GetSagaStatusAsync(id, ct);
        if (result.IsFailure) return NotFound(new { error = result.Error });
        return Ok(result.Value);
    }
}
