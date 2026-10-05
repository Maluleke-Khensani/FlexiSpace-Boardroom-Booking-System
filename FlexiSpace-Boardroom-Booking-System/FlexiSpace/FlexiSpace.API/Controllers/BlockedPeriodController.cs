using FlexiSpace.API.Authorization;
using FlexiSpace.Core.Common;
using FlexiSpace.Core.DTOs.BlockedPeriod;
using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Enums;
using FlexiSpace.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlexiSpace.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BlockedPeriodController(
    ApplicationDbContext context,
    ICurrentUserService currentUserService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? boardroomId, [FromQuery] int? locationId)
    {
        var user = await currentUserService.GetCurrentUserAsync();
        if (user is null) return Unauthorized();

        var query = context.BlockedPeriods
            .Include(b => b.Boardroom)
            .Include(b => b.CreatedByUser)
            .AsQueryable();

        if (user.Role == UserRole.CentreManager)
            query = query.Where(b => b.Boardroom != null && b.Boardroom.LocationId == user.LocationId);

        if (boardroomId is not null)
            query = query.Where(b => b.BoardroomId == boardroomId);

        if (locationId is not null)
            query = query.Where(b => b.Boardroom != null && b.Boardroom.LocationId == locationId);

        var items = await query
            .OrderByDescending(b => b.Start)
            .ToListAsync();

        return Ok(items.Select(Map));
    }

    [HttpPost]
    [AuthorizeRoles(UserRole.CentreManager, UserRole.Administrator)]
    public async Task<IActionResult> Create(CreateBlockedPeriodDto dto)
    {
        var user = await currentUserService.GetCurrentUserAsync();
        if (user is null) return Unauthorized();

        var boardroom = await context.Boardrooms.FindAsync(dto.BoardroomId);
        if (boardroom is null)
            return NotFound(new { message = "Boardroom not found." });

        if (user.Role == UserRole.CentreManager && boardroom.LocationId != user.LocationId)
            return Forbid();

        var start = dto.StartDate.ToDateTime(dto.StartTime);
        var end = dto.EndDate.ToDateTime(dto.EndTime);
        if (end <= start)
            return BadRequest(new { message = "End must be after start." });

        var block = new BlockedPeriod
        {
            BoardroomId = dto.BoardroomId,
            Start = start,
            End = end,
            Reason = string.IsNullOrWhiteSpace(dto.Reason) ? "Blocked by Centre Manager" : dto.Reason.Trim(),
            CreatedById = user.Id
        };

        context.BlockedPeriods.Add(block);
        await context.SaveChangesAsync();

        await context.Entry(block).Reference(b => b.Boardroom).LoadAsync();
        await context.Entry(block).Reference(b => b.CreatedByUser).LoadAsync();

        return CreatedAtAction(nameof(GetAll), new { id = block.Id }, Map(block));
    }

    [HttpDelete("{id:int}")]
    [AuthorizeRoles(UserRole.CentreManager, UserRole.Administrator)]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await currentUserService.GetCurrentUserAsync();
        if (user is null) return Unauthorized();

        var block = await context.BlockedPeriods
            .Include(b => b.Boardroom)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (block is null) return NotFound();

        if (user.Role == UserRole.CentreManager &&
            (block.Boardroom is null || block.Boardroom.LocationId != user.LocationId))
            return Forbid();

        context.BlockedPeriods.Remove(block);
        await context.SaveChangesAsync();
        return NoContent();
    }

    private static BlockedPeriodResponseDto Map(BlockedPeriod b) => new()
    {
        Id = b.Id,
        BoardroomId = b.BoardroomId,
        BoardroomName = b.Boardroom?.Name ?? string.Empty,
        LocationId = b.Boardroom?.LocationId ?? 0,
        StartDate = DateOnly.FromDateTime(b.Start),
        StartTime = TimeOnly.FromDateTime(b.Start),
        EndDate = DateOnly.FromDateTime(b.End),
        EndTime = TimeOnly.FromDateTime(b.End),
        Reason = b.Reason,
        CreatedByUserId = b.CreatedById,
        CreatedByName = b.CreatedByUser is null
            ? string.Empty
            : $"{b.CreatedByUser.FirstName} {b.CreatedByUser.LastName}".Trim()
    };
}
