namespace FlexiSpace.Core.DTOs.Catering
{
    public class CateringUpdateDto
    {
        public required string Name { get; set; }

        public string? Description { get; set; }

        public bool IsActive { get; set; }
    }
}