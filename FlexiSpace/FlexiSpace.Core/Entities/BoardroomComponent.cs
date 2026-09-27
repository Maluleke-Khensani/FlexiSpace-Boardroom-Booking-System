namespace FlexiSpace.Core.Entities
{
    // Records one "conjoined" relationship: ComponentBoardroom is one of the
    // physical rooms that combine to form CombinedBoardroom (e.g. Eagle
    // Canyon's Thingamajik + Whachamacallit combine into one bigger space
    // when the partition between them is opened - this mirrors the room
    // combination feature already shipped in the mobile app). The combined
    // boardroom is itself an ordinary Boardroom row with its own Id, Name
    // and Capacity - this table only records which rooms make it up.
    public class BoardroomComponent
    {
        public int CombinedBoardroomId { get; set; }

        public Boardroom? CombinedBoardroom { get; set; }

        public int ComponentBoardroomId { get; set; }

        public Boardroom? ComponentBoardroom { get; set; }
    }
}
