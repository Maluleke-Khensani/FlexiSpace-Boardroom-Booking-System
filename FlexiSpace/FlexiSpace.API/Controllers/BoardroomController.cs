using FlexiSpace.API.Authorization;
using FlexiSpace.API.Controllers.Base;
using FlexiSpace.Core.Common;
using FlexiSpace.Core.DTOs.Boardroom;
using FlexiSpace.Core.DTOs.Equipment;
using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Enums;
using FlexiSpace.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlexiSpace.API.Controllers
{
    // Any authenticated FlexiSpace user can read boardrooms (they need to
    // see rooms to book them). Creating/editing/deleting a boardroom is
    // catalogue management, not booking - restricted to Administrators,
    // same rule as Equipment/Catering/Location.
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BoardroomController : AuditableControllerBase
    {
        // Service responsible for handling all boardroom-related business operations.
        private readonly IBoardroomService _boardroomService;

        public BoardroomController(
            IBoardroomService boardroomService,
            IAuditService auditService,
            ICurrentUserService currentUserService)
            : base(auditService, currentUserService)
        {
            _boardroomService = boardroomService;
        }

        // Retrieves all boardrooms together with their assigned equipment.
        [HttpGet]
        public async Task<IActionResult> GetAllBoardrooms()
        {
            var boardrooms = await _boardroomService.GetAllBoardroomsAsync();

            var response = boardrooms.Select(MapToResponseDto);

            return Ok(response);
        }

        // Retrieves a single boardroom using its unique ID.
        [HttpGet("{id}")]
        public async Task<IActionResult> GetBoardroomById(int id)
        {
            var boardroom = await _boardroomService.GetBoardroomByIdAsync(id);

            if (boardroom == null)
            {
                return NotFound();
            }

            return Ok(MapToResponseDto(boardroom));
        }

        [AuthorizeRoles(UserRole.Administrator, UserRole.CentreManager)]
        [HttpPost]
        public async Task<IActionResult> CreateBoardroom(BoardroomCreateDto dto)
        {
            var boardroom = new Boardroom
            {
                Name = dto.Name,
                Capacity = dto.Capacity,
                Status = dto.Status,
                LocationId = dto.LocationId
            };

            foreach (var item in dto.Equipment)
            {
                boardroom.BoardroomEquipments.Add(new BoardroomEquipment
                {
                    Boardroom = boardroom,
                    EquipmentId = item.EquipmentId,
                    Quantity = item.Quantity
                });
            }

            // CreateBoardroomAsync can throw BusinessRuleException
            // (duplicate name at this location, or an unknown EquipmentId).
            try
            {
                var createdBoardroom = await _boardroomService.CreateBoardroomAsync(boardroom);

                await LogActionAsync(
                    AuditAction.Create,
                    nameof(Boardroom),
                    createdBoardroom.Id.ToString(),
                    newValues: new
                    {
                        createdBoardroom.Name,
                        createdBoardroom.Capacity,
                        Status = createdBoardroom.Status.ToString(),
                        createdBoardroom.LocationId
                    });

                return CreatedAtAction(
                    nameof(GetBoardroomById),
                    new { id = createdBoardroom.Id },
                    MapToResponseDto(createdBoardroom));
            }
            catch (BusinessRuleException ex)
            {
                return BadRequest(new { errors = ex.Errors });
            }
        }

        // Updates an existing boardroom and replaces its equipment list.
        // Administrator-only: see class summary above.
        [AuthorizeRoles(UserRole.Administrator)]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateBoardroom(int id, BoardroomUpdateDto dto)
        {
            // Fetch the existing state first so the audit log can record
            // what changed, not just what it changed to.
            var before = await _boardroomService.GetBoardroomByIdAsync(id);

            if (before == null)
            {
                return NotFound();
            }

            var boardroom = new Boardroom
            {
                Name = dto.Name,
                Capacity = dto.Capacity,
                Status = dto.Status,
                LocationId = dto.LocationId
            };

            var equipment = dto.Equipment.Select(item => new BoardroomEquipment
            {
                Boardroom = boardroom,
                EquipmentId = item.EquipmentId,
                Quantity = item.Quantity
            }).ToList();

            // UpdateBoardroomAsync can throw BusinessRuleException too.
            try
            {
                var updated = await _boardroomService.UpdateBoardroomAsync(id, boardroom, equipment);

                if (!updated)
                {
                    return NotFound();
                }

                await LogActionAsync(
                    AuditAction.Update,
                    nameof(Boardroom),
                    id.ToString(),
                    oldValues: new
                    {
                        before.Name,
                        before.Capacity,
                        Status = before.Status.ToString(),
                        before.LocationId
                    },
                    newValues: new
                    {
                        dto.Name,
                        dto.Capacity,
                        Status = dto.Status.ToString(),
                        dto.LocationId
                    });

                return NoContent();
            }
            catch (BusinessRuleException ex)
            {
                return BadRequest(new { errors = ex.Errors });
            }
        }

        // Deletes a boardroom from the system.
        // Administrator-only: this is the exact example the team used when
        // scoping RBAC ("only Admins can delete a room").
        [AuthorizeRoles(UserRole.Administrator)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBoardroom(int id)
        {
            var before = await _boardroomService.GetBoardroomByIdAsync(id);

            if (before == null)
            {
                return NotFound();
            }

            // DeleteBoardroomAsync can throw BusinessRuleException
            // (existing bookings reference this boardroom).
            try
            {
                var deleted = await _boardroomService.DeleteBoardroomAsync(id);

                if (!deleted)
                {
                    return NotFound();
                }

                await LogActionAsync(
                    AuditAction.Delete,
                    nameof(Boardroom),
                    id.ToString(),
                    oldValues: new
                    {
                        before.Name,
                        before.Capacity,
                        Status = before.Status.ToString(),
                        before.LocationId
                    });

                return NoContent();
            }
            catch (BusinessRuleException ex)
            {
                return BadRequest(new { errors = ex.Errors });
            }
        }

        // Sets which boardrooms combine to form this one - e.g. linking
        // "Thingamajik" and "Whachamacallit" as the components of a bigger
        // conjoined room (mirrors the combination feature already shipped
        // in the mobile app). Replaces the existing component list.
        [HttpPut("{id}/components")]
        public async Task<IActionResult> SetBoardroomComponents(int id, BoardroomComponentsDto dto)
        {
            try
            {
                var updated = await _boardroomService.SetBoardroomComponentsAsync(id, dto.ComponentBoardroomIds);

                if (!updated)
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (BusinessRuleException ex)
            {
                return BadRequest(new { errors = ex.Errors });
            }
        }

        // Converts a Boardroom entity into a response DTO.
        // Keeping the mapping in one place avoids repeating the same code in multiple endpoints.
        private static BoardroomResponseDto MapToResponseDto(Boardroom boardroom)
        {
            return new BoardroomResponseDto
            {
                Id = boardroom.Id,
                Name = boardroom.Name,
                Capacity = boardroom.Capacity,
                Status = boardroom.Status,
                LocationId = boardroom.LocationId,

                Equipment = boardroom.BoardroomEquipments
                    .Select(be => new BoardroomEquipmentDto
                    {
                        EquipmentId = be.EquipmentId,
                        Quantity = be.Quantity
                    })
                    .ToList(),

                ComponentBoardroomIds = boardroom.Components
                    .Select(c => c.ComponentBoardroomId)
                    .ToList(),

                CombinedIntoBoardroomIds = boardroom.PartOfCombinations
                    .Select(c => c.CombinedBoardroomId)
                    .ToList()
            };
        }
    }
}
