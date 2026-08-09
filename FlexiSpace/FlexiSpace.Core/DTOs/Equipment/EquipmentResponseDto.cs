namespace FlexiSpace.Core.DTOs.Equipment
{
    public class EquipmentResponseDto
    {
        public int Id { get; set; }

        public required string Name { get; set; }

        public required string Description { get; set; }

        public bool IsActive { get; set; }
    }
}