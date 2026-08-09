using FlexiSpace.Core.Enums;

namespace FlexiSpace.Core.DTOs.Boardroom
{
    public class BoardroomUpdateDto
    {
        public required string Name { get; set; }

        public int Capacity { get; set; }

        public BoardroomStatus Status { get; set; }

        public int LocationId { get; set; }

        public List<BoardroomEquipmentDto> Equipment { get; set; } = new();

    }
}