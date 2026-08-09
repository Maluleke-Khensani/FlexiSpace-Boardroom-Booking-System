namespace FlexiSpace.Core.DTOs.Catering
{
    public class CateringUpdateDto
    {
        public required string Name { get; set; }

        public required string Description { get; set; }

        public bool IsActive { get; set; }
    }
}