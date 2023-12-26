using LibrarySystem.DTO;

namespace LibrarySystem.Interfaces
{
    public interface ILibrarySystemService
    {
        Task<bool> HealthCheckAsync(string base_adress);
        Task<PaginationResponse<LibraryResponse>> GetCityLibraries(int? page, int? size, string city);
        Task<PaginationResponse<LibraryBookResponse>> GetLibraryBooks(int? page, int? size, Guid libraryGuid, bool? allShow = false);
        Task<RatingResponse?> GetRatingResponseByUserName(string userName);
        Task<object> CreateBookReservation(string userName, RentingRequest request);

        Task<IEnumerable<OpenReservationResponse>> GetBookReservations(string userName);
        Task<CloseReservationResponse> CloseBookReservation(string userName, Guid reservationUid, ReturnBookRequest request);
    }
}
