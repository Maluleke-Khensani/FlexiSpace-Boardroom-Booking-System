using FlexiSpace.API.Authorization;
using FlexiSpace.Core.Common;
using FlexiSpace.Core.DTOs.BlockedPeriod;
using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Enums;
using FlexiSpace.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FlexiSpace.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BlockedPeriodController : ControllerBase
    {
        private readonly IBlockedPeriodService _blockedPeriodService;

        public BlockedPeriodController(IBlockedPeriodService blockedPeriodService)
        {
            _blockedPeriodService = blockedPeriodService;
        }

        // Lists blocks, soonest first. Any authenticated user can read
        // them - clients need them to show a room as blocked. Optional
        // filters: ?boardroomId=1&from=2026-10-01T00:00:00&to=2026-10-31T23:59:00
        [HttpGet]
        public async Task<IActionResult> GetBlockedPeriods(
            [FromQuery] int? boardroomId,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
        {
            var blockedPeriods = await _blockedPeriodService.GetBlockedPeriodsAsync(boardroomId, from, to);

            return Ok(blockedPeriods.Select(MapToResponseDto));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetBlockedPeriodById(int id)
        {
            var blockedPeriod = await _blockedPeriodService.GetBlockedPeriodByIdAsync(id);

            if (blockedPeriod == null)
            {
                return NotFound();
            }

            return Ok(MapToResponseDto(blockedPeriod));
        }

        // Blocks a boardroom for a period. Administrators can block any
        // boardroom; Centre Managers only at their own location (checked
        // again in the service, since the role attribute alone can't see
        // which boardroom the request is about).
        [AuthorizeRoles(UserRole.CentreManager, UserRole.Administrator)]
        [HttpPost]
        public async Task<IActionResult> CreateBlockedPeriod(BlockedPeriodCreateDto dto)
        {
            var blockedPeriod = new BlockedPeriod
            {
                BoardroomId = dto.BoardroomId,
                Start = dto.Start,
                End = dto.End,
                Reason = dto.Reason
            };

            try
            {
                var created = await _blockedPeriodService.CreateBlockedPeriodAsync(blockedPeriod);

                return CreatedAtAction(
                    nameof(GetBlockedPeriodById),
                    new { id = created.Id },
                    MapToResponseDto(created));
            }
            catch (ForbiddenException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (BusinessRuleException ex)
            {
                return BadRequest(new { errors = ex.Errors });
            }
        }

        [AuthorizeRoles(UserRole.CentreManager, UserRole.Administrator)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBlockedPeriod(int id)
        {
            try
            {
                var deleted = await _blockedPeriodService.DeleteBlockedPeriodAsync(id);

                if (!deleted)
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (ForbiddenException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
        }

        private static BlockedPeriodResponseDto MapToResponseDto(BlockedPeriod blockedPeriod)
        {
            return new BlockedPeriodResponseDto
            {
                Id = blockedPeriod.Id,
                BoardroomId = blockedPeriod.BoardroomId,
                Start = blockedPeriod.Start,
                End = blockedPeriod.End,
                Reason = blockedPeriod.Reason,
                CreatedById = blockedPeriod.CreatedById,
                CreatedByName = blockedPeriod.CreatedBy is null
                    ? string.Empty
                    : $"{blockedPeriod.CreatedBy.FirstName} {blockedPeriod.CreatedBy.LastName}".Trim(),
                CreatedAt = blockedPeriod.CreatedAt
            };
        }
    }
}
