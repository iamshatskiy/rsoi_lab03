namespace LibrarySystem.DTO
{
    public class OpenReservationResponse
    {
        public Guid? reservationUid { get; set; }
        public string? status { get; set; }
        public DateOnly? startDate { get; set; }
        public DateOnly? tillDate { get; set; }
        public object? book { get; set; }
        public object? library { get; set; }
    }
}
