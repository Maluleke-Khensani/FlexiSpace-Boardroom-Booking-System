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

    }
}