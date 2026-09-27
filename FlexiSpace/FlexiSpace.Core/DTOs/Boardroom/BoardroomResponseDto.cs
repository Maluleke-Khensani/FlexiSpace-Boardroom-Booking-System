using FlexiSpace.Core.Enums;

namespace FlexiSpace.Core.DTOs.Boardroom
{
    public class BoardroomResponseDto
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public int Capacity { get; set; }

        public BoardroomStatus Status { get; set; }

        public int LocationId { get; set; }

        public List<BoardroomEquipmentDto> Equipment { get; set; } = new();

        // Populated if this boardroom IS a combined space - the rooms that
        // make it up (e.g. Thingamajik + Whachamacallit).
        public List<int> ComponentBoardroomIds { get; set; } = new();

        // Populated if this boardroom can itself be conjoined with others -
        // the combined boardroom(s) it's a component of.
        public List<int> CombinedIntoBoardroomIds { get; set; } = new();

    }
}