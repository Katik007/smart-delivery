using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartDelivery.DeliveryService.Application.DTOs;
using SmartDelivery.DeliveryService.Domain;
using SmartDelivery.DeliveryService.Infrastructure.Data;
using SmartDelivery.Shared.Security;

namespace SmartDelivery.DeliveryService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DeliveryController : ControllerBase
{
    private readonly DeliveryDbContext _dbContext;
    private readonly ILogger<DeliveryController> _logger;

    public DeliveryController(DeliveryDbContext dbContext, ILogger<DeliveryController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet("tasks")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<DeliveryTaskDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTasks([FromQuery] DeliveryTaskStatus? status, CancellationToken ct)
    {
        var query = _dbContext.DeliveryTasks.AsNoTracking();
        if (status.HasValue)
        {
            query = query.Where(t => t.Status == status.Value);
        }

        var tasks = await query.OrderByDescending(t => t.CreatedAtUtc).ToListAsync(ct);
        var dtos = tasks.Select(MapTaskToDto).ToList();
        return Ok(dtos);
    }

    [HttpGet("tasks/{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(DeliveryTaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTaskById(Guid id, CancellationToken ct)
    {
        var task = await _dbContext.DeliveryTasks.FindAsync(new object[] { id }, ct);
        if (task == null) return NotFound(new { error = "Delivery task not found." });
        return Ok(MapTaskToDto(task));
    }

    [HttpGet("tasks/by-order/{orderId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(DeliveryTaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTaskByOrderId(Guid orderId, CancellationToken ct)
    {
        var task = await _dbContext.DeliveryTasks.FirstOrDefaultAsync(t => t.OrderId == orderId, ct);
        if (task == null) return NotFound(new { error = "Delivery task for order not found." });
        return Ok(MapTaskToDto(task));
    }

    [HttpPost("tasks/{id:guid}/assign")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Manager}")]
    [ProducesResponseType(typeof(DeliveryTaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignCourier(Guid id, [FromBody] AssignCourierRequest request, CancellationToken ct)
    {
        var task = await _dbContext.DeliveryTasks.FindAsync(new object[] { id }, ct);
        if (task == null) return NotFound(new { error = "Task not found." });

        var courier = await _dbContext.Couriers.FindAsync(new object[] { request.CourierId }, ct);
        if (courier == null) return NotFound(new { error = "Courier not found." });

        task.AssignCourier(courier.Id);
        courier.SetStatus(CourierStatus.Busy);

        await _dbContext.SaveChangesAsync(ct);
        return Ok(MapTaskToDto(task));
    }

    [HttpPost("tasks/{id:guid}/status")]
    [Authorize(Roles = $"{Roles.Courier},{Roles.Admin}")]
    [ProducesResponseType(typeof(DeliveryTaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTaskStatus(Guid id, [FromBody] UpdateTaskStatusRequest request, CancellationToken ct)
    {
        var task = await _dbContext.DeliveryTasks.FindAsync(new object[] { id }, ct);
        if (task == null) return NotFound(new { error = "Task not found." });

        try
        {
            switch (request.Status)
            {
                case DeliveryTaskStatus.InTransit:
                    task.StartTransit();
                    break;
                case DeliveryTaskStatus.Delivered:
                    task.CompleteDelivery();
                    if (task.CourierId.HasValue)
                    {
                        var courier = await _dbContext.Couriers.FindAsync(new object[] { task.CourierId.Value }, ct);
                        courier?.SetStatus(CourierStatus.Available);
                    }
                    break;
                case DeliveryTaskStatus.Failed:
                    task.FailDelivery(request.FailureReason ?? "Undelivered");
                    break;
                default:
                    return BadRequest(new { error = "Invalid status transition" });
            }

            await _dbContext.SaveChangesAsync(ct);
            return Ok(MapTaskToDto(task));
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("couriers")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Manager}")]
    [ProducesResponseType(typeof(IReadOnlyList<CourierDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCouriers(CancellationToken ct)
    {
        var couriers = await _dbContext.Couriers.AsNoTracking().ToListAsync(ct);
        return Ok(couriers.Select(MapCourierToDto).ToList());
    }

    [HttpPost("couriers")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(typeof(CourierDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateCourier([FromBody] CreateCourierRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.PhoneNumber))
            return BadRequest(new { error = "Full name and phone number are required." });

        var courier = new Courier(
            Guid.NewGuid(),
            request.FullName,
            request.PhoneNumber,
            request.Vehicle,
            request.CurrentZone
        );

        _dbContext.Couriers.Add(courier);
        await _dbContext.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetCouriers), new { id = courier.Id }, MapCourierToDto(courier));
    }

    private static DeliveryTaskDto MapTaskToDto(DeliveryTask t) =>
        new(
            t.Id,
            t.OrderId,
            t.CustomerId,
            t.DeliveryAddress,
            t.TotalAmount,
            t.Status.ToString(),
            t.CourierId,
            t.FailureReason,
            t.CreatedAtUtc,
            t.AssignedAtUtc,
            t.DeliveredAtUtc
        );

    private static CourierDto MapCourierToDto(Courier c) =>
        new(
            c.Id,
            c.FullName,
            c.PhoneNumber,
            c.Vehicle.ToString(),
            c.Status.ToString(),
            c.CurrentZone,
            c.CreatedAtUtc
        );
}
