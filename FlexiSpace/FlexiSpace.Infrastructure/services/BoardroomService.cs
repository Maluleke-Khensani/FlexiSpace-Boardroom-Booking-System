using FlexiSpace.Core.Common;
using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Enums;
using FlexiSpace.Core.Services;
using FlexiSpace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FlexiSpace.Infrastructure.Services
{
    public class BoardroomService : IBoardroomService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public BoardroomService(ApplicationDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<IEnumerable<Boardroom>> GetAllBoardroomsAsync()
        {
            return await _context.Boardrooms
                .Include(b => b.Location)
                .Include(b => b.BoardroomEquipments)
                    .ThenInclude(be => be.Equipment)
                .Include(b => b.Components)
                .Include(b => b.PartOfCombinations)
                .ToListAsync();
        }

        public async Task<Boardroom?> GetBoardroomByIdAsync(int id)
        {
            return await _context.Boardrooms
                .Include(b => b.Location)
                .Include(b => b.BoardroomEquipments)
                    .ThenInclude(be => be.Equipment)
                .Include(b => b.Components)
                .Include(b => b.PartOfCombinations)
                .FirstOrDefaultAsync(b => b.Id == id);
        }

        // Sets which boardrooms combine to form this one, e.g. linking
        // Thingamajik and Whachamacallit as the components of one bigger
        // conjoined space (mirrors the room combination feature already
        // shipped in the mobile app). Fully replaces the existing list.
        public async Task<bool> SetBoardroomComponentsAsync(int combinedBoardroomId, List<int> componentBoardroomIds)
        {
            var combinedBoardroom = await _context.Boardrooms
                .Include(b => b.Components)
                .FirstOrDefaultAsync(b => b.Id == combinedBoardroomId);

            if (combinedBoardroom == null)
            {
                return false;
            }

            // CentreManagers may only combine rooms at their own
            // location; Administrators can combine anything.
            await EnsureCanManageLocationAsync(combinedBoardroom.LocationId);

            var distinctIds = componentBoardroomIds.Distinct().ToList();

            var errors = new List<string>();

            if (distinctIds.Contains(combinedBoardroomId))
            {
                errors.Add("A boardroom can't be listed as its own component.");
            }

            if (distinctIds.Count > 0)
            {
                var existingIds = await _context.Boardrooms
                    .Where(b => distinctIds.Contains(b.Id))
                    .Select(b => b.Id)
                    .ToListAsync();

                foreach (var missingId in distinctIds.Except(existingIds))
                {
                    errors.Add($"Boardroom {missingId} was not found.");
                }

                // A component can't already belong to a different combination.
                var conflictingComponents = await _context.BoardroomComponents
                    .Where(bc => distinctIds.Contains(bc.ComponentBoardroomId)
                                 && bc.CombinedBoardroomId != combinedBoardroomId)
                    .Select(bc => bc.ComponentBoardroomId)
                    .ToListAsync();

                foreach (var id in conflictingComponents)
                {
                    errors.Add($"Boardroom {id} is already part of a different combination.");
                }

                // A component can't itself be a combined boardroom - no
                // nested/chained combinations.
                var alreadyCombinedRooms = await _context.BoardroomComponents
                    .Where(bc => distinctIds.Contains(bc.CombinedBoardroomId))
                    .Select(bc => bc.CombinedBoardroomId)
                    .Distinct()
                    .ToListAsync();

                foreach (var id in alreadyCombinedRooms)
                {
                    errors.Add($"Boardroom {id} is itself a combined boardroom and can't be used as a component.");
                }
            }

            if (errors.Count > 0)
            {
                throw new BusinessRuleException(errors);
            }

            _context.BoardroomComponents.RemoveRange(combinedBoardroom.Components);

            foreach (var componentId in distinctIds)
            {
                _context.BoardroomComponents.Add(new BoardroomComponent
                {
                    CombinedBoardroomId = combinedBoardroomId,
                    ComponentBoardroomId = componentId
                });
            }

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<Boardroom> CreateBoardroomAsync(Boardroom boardroom)
        {
            // A CentreManager can only ever create a boardroom at
            // their own location - whatever LocationId was submitted is
            // overridden rather than trusted, the same way a non-manager's
            // submitted UserId is overridden on booking creation.
            // Administrators are unrestricted.
            var currentUser = await _currentUserService.GetCurrentUserAsync();

            if (currentUser == null)
            {
                throw new ForbiddenException("You must be signed in to create a boardroom.");
            }

            if (currentUser.Role == UserRole.CentreManager)
            {
                if (currentUser.LocationId == null)
                {
                    throw new ForbiddenException("Your account has no assigned location, so you can't create boardrooms.");
                }

                boardroom.LocationId = currentUser.LocationId.Value;
            }

            // Collects every violation into one list, same pattern
            // SetBoardroomComponentsAsync above already uses, so the
            // caller sees all of them in one response.
            var errors = new List<string>();

            // Duplicate-name guard, scoped to this boardroom's own
            // location.
            var nameTaken = await _context.Boardrooms
                .AnyAsync(b => b.LocationId == boardroom.LocationId
                    && b.Name.ToLower() == boardroom.Name.Trim().ToLower());

            if (nameTaken)
            {
                errors.Add($"A boardroom named '{boardroom.Name}' already exists at this location.");
            }

            // Equipment-ID validation - every EquipmentId attached
            // must actually exist.
            errors.AddRange(await ValidateEquipmentIdsExistAsync(boardroom.BoardroomEquipments));

            if (errors.Count > 0)
            {
                throw new BusinessRuleException(errors);
            }

            _context.Boardrooms.Add(boardroom);

            await _context.SaveChangesAsync();

            return boardroom;
        }

        public async Task<bool> UpdateBoardroomAsync(int id, Boardroom updatedBoardroom, List<BoardroomEquipment> equipment)
        {
            var existingBoardroom = await _context.Boardrooms
                .Include(b => b.BoardroomEquipments)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (existingBoardroom == null)
            {
                return false;
            }

            // A CentreManager may only update a boardroom that's
            // already at their own location, and can't move it to a
            // different one - whatever LocationId was submitted is
            // overridden to their own, same as on create.
            var currentUser = await _currentUserService.GetCurrentUserAsync();

            if (currentUser == null)
            {
                throw new ForbiddenException("You must be signed in to update a boardroom.");
            }

            if (currentUser.Role == UserRole.CentreManager)
            {
                if (currentUser.LocationId == null || existingBoardroom.LocationId != currentUser.LocationId)
                {
                    throw new ForbiddenException("You can only manage boardrooms at your own location.");
                }

                updatedBoardroom.LocationId = currentUser.LocationId.Value;
            }

            // Same two guards as CreateBoardroomAsync above.
            var errors = new List<string>();

            var nameTaken = await _context.Boardrooms
                .AnyAsync(b => b.Id != id
                    && b.LocationId == updatedBoardroom.LocationId
                    && b.Name.ToLower() == updatedBoardroom.Name.Trim().ToLower());

            if (nameTaken)
            {
                errors.Add($"A boardroom named '{updatedBoardroom.Name}' already exists at this location.");
            }

            errors.AddRange(await ValidateEquipmentIdsExistAsync(equipment));

            if (errors.Count > 0)
            {
                throw new BusinessRuleException(errors);
            }

            // Update boardroom details
            existingBoardroom.Name = updatedBoardroom.Name;
            existingBoardroom.Capacity = updatedBoardroom.Capacity;
            existingBoardroom.Status = updatedBoardroom.Status;
            existingBoardroom.LocationId = updatedBoardroom.LocationId;

            // Remove existing equipment
            _context.BoardroomEquipments.RemoveRange(existingBoardroom.BoardroomEquipments);

            // Add the new equipment
            existingBoardroom.BoardroomEquipments = equipment;

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> DeleteBoardroomAsync(int id)
        {
            var boardroom = await _context.Boardrooms.FindAsync(id);

            if (boardroom == null)
            {
                return false;
            }

            // CentreManagers can only delete boardrooms at their own
            // location; Administrators can delete any.
            await EnsureCanManageLocationAsync(boardroom.LocationId);

            // Delete guard. A boardroom with existing bookings can't
            // be removed outright, since that would delete booking
            // history along with it.
            var hasBookings = await _context.Bookings
                .AnyAsync(bk => bk.BoardroomId == id);

            if (hasBookings)
            {
                throw new BusinessRuleException(
                    "This boardroom can't be deleted because it still has bookings recorded against it. " +
                    "Mark it as Unavailable instead if it's no longer in use.");
            }

            _context.Boardrooms.Remove(boardroom);

            await _context.SaveChangesAsync();

            return true;
        }

        // Shared location-scoping check. Administrator: always
        // allowed. CentreManager: only for boardrooms at their own
        // LocationId. Anyone else: never (they shouldn't be hitting these
        // methods at all - the controller's [AuthorizeRoles] already
        // blocks them, this is defence in depth).
        private async Task EnsureCanManageLocationAsync(int locationId)
        {
            var currentUser = await _currentUserService.GetCurrentUserAsync();

            if (currentUser == null)
            {
                throw new ForbiddenException("You must be signed in to manage boardrooms.");
            }

            if (currentUser.Role == UserRole.Administrator)
            {
                return;
            }

            if (currentUser.Role == UserRole.CentreManager && currentUser.LocationId == locationId)
            {
                return;
            }

            throw new ForbiddenException("You can only manage boardrooms at your own location.");
        }

        // Shared helper used by both CreateBoardroomAsync and
        // UpdateBoardroomAsync so the equipment-ID check stays identical
        // in both places.
        private async Task<List<string>> ValidateEquipmentIdsExistAsync(
            IEnumerable<BoardroomEquipment> boardroomEquipments)
        {
            var errors = new List<string>();

            var equipmentIds = boardroomEquipments
                .Select(be => be.EquipmentId)
                .Distinct()
                .ToList();

            if (equipmentIds.Count == 0)
            {
                return errors;
            }

            var existingIds = await _context.Equipments
                .Where(e => equipmentIds.Contains(e.Id))
                .Select(e => e.Id)
                .ToListAsync();

            foreach (var missingId in equipmentIds.Except(existingIds))
            {
                errors.Add($"Equipment {missingId} was not found.");
            }

            return errors;
        }
    }
}
