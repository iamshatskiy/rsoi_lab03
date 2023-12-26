using LibrarySystem.DTO;
using LibrarySystem.Interfaces;
using LibrarySystem.Models;
using LibrarySystem.Utils;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Drawing;
using System.Net;

namespace LibrarySystem.Services
{
    public class LibrarySystemService : ILibrarySystemService
    {
        private readonly HttpClient _httpClient;
        private readonly CircuitBreaker _circuitBreaker;
        private readonly RequestQueueService _requestQueueService;

        public LibrarySystemService()
        {
            _httpClient = new HttpClient();
            _circuitBreaker = CircuitBreaker.Instance;
            _requestQueueService = new RequestQueueService();
            _requestQueueService.StartWorker();
        }

        public async Task<bool> HealthCheckAsync(string base_adress)
        {
            if (_circuitBreaker.IsOpened())
            {
                return false;
            }
            using var req = new HttpRequestMessage(HttpMethod.Get,
                $"http://{base_adress}/manage/health");
            try
            {
                using var res = await _httpClient.SendAsync(req);
                _circuitBreaker.ResetFailureCount();
                return res.StatusCode == HttpStatusCode.OK;
            }
            catch (Exception e)
            {
                _circuitBreaker.IncrementFailureCount();
                if (_circuitBreaker.IsOpened())
                {
                    var reqClone = await HttpRequestMessageHelper.CloneHttpRequestMessageAsync(req);
                    _requestQueueService.AddRequestToQueue(reqClone);
                }
                return false;
            }
        }

        //DONE
        public async Task<PaginationResponse<LibraryResponse>> GetCityLibraries(int? page, int? size, string city)
        {
            if (_circuitBreaker.IsOpened())
            {
                return null;
            }

            var result = await _httpClient.GetFromJsonAsync<PaginationResponse<LibraryResponse>>($"http://library:8060/libraries?page={page}&size={size}&city={city}");

            return result;
        }

        //DONE
        public async Task<PaginationResponse<LibraryBookResponse>> GetLibraryBooks(int? page, int? size, Guid libraryGuid, bool? allShow = false)
        {
            if (_circuitBreaker.IsOpened())
            {
                return null;
            }

            var result = await _httpClient.GetFromJsonAsync<PaginationResponse<LibraryBookResponse>>($"http://library:8060/libraries/{libraryGuid}/books?page={page}&size={size}&allShow={allShow}");

            return result;
        }

        //DONE
        public async Task<RatingResponse?> GetRatingResponseByUserName(string userName)
        {
            if (_circuitBreaker.IsOpened())
            {
                return null;
            }
            using var ratingRequest = new HttpRequestMessage(HttpMethod.Get, $"http://rating:8050/rating");
            ratingRequest.Headers.Add("X-User-Name", userName);
            using var ratingResult = await _httpClient.SendAsync(ratingRequest);
            var rating = await ratingResult.Content.ReadFromJsonAsync<RatingResponse>();

            return rating;
        }

        public async Task<object> CreateBookReservation(string userName, RentingRequest request)
        {

            var checkLibrary = HealthCheckAsync("library:8060");
            if (!await checkLibrary)
            {
                return "Library service unavailable";
            }
            var bookCheck = await _httpClient.GetFromJsonAsync<CheckResponse>($"http://library:8060/library/checkBookAvailable/?libraryUid={request.libraryUid}&bookUid={request.bookUid}");

            if (bookCheck?.check == false)
            {
                return null;
            }


            var checkRating = HealthCheckAsync("rating:8050");
            if (!await checkRating)
            {
                return "Bonus service unavailable";
            }
            using var ratingRequest = new HttpRequestMessage(HttpMethod.Get, $"http://rating:8050/rating");
            ratingRequest.Headers.Add("X-User-Name", userName);
            using var ratingResult = await _httpClient.SendAsync(ratingRequest);
            var rating = await ratingResult.Content.ReadFromJsonAsync<RatingResponse>();

            var checkReservation = HealthCheckAsync("reservation:8070");
            if (!await checkReservation)
            {
                return "Reservation service unavailable";
            }
            using var reserveCountRequest = new HttpRequestMessage(HttpMethod.Get, $"http://reservation:8070/reservationsCount");
            reserveCountRequest.Headers.Add("X-User-Name", userName);
            using var reserveCountResult = await _httpClient.SendAsync(reserveCountRequest);
            reserveCountResult.EnsureSuccessStatusCode();
            var reservationCount = await reserveCountResult.Content.ReadFromJsonAsync<ReservationCountResponse>();


            if (reservationCount.reservationCount >= rating.Stars)
            {
                return null;
            }

            if (!await checkReservation)
            {
                return "Reservation service unavailable";
            }

            var reserveRequest = new HttpRequestMessage(HttpMethod.Post, "http://reservation:8070/reservation/create");
            reserveRequest.Headers.Add("X-User-Name", userName);
            reserveRequest.Content = JsonContent.Create(request, typeof(RentingRequest));

            var reserveResult = await _httpClient.SendAsync(reserveRequest);
            reserveResult.EnsureSuccessStatusCode();
            var reservation = await reserveResult.Content.ReadFromJsonAsync<ReservationResponse>();


            checkLibrary = HealthCheckAsync("library:8060");

            if (!await checkLibrary)
            {
                var reserveDelete = new HttpRequestMessage(HttpMethod.Delete, "http://reservation:8070/reservation/delete");
                var requestReturnBody = new ReturnRequest { reservationGuid = reservation.Reservation_uid.ToString(), returnDate = reservation.Till_date };
                reserveDelete.Content = JsonContent.Create(requestReturnBody, typeof(ReturnRequest));
                var deleteResult = await _httpClient.SendAsync(reserveDelete);
                deleteResult.EnsureSuccessStatusCode();

                return "Library service unavailable";
            }

            var libraryRequestBody = new RentRequest { bookUid = request.bookUid, libraryUid = request.libraryUid };
            var action = true; //Флаг взятия книги

            var libraryRequest = new HttpRequestMessage(HttpMethod.Post, $"http://library:8060/library/rentBook?action={action}");
            libraryRequest.Content = JsonContent.Create(libraryRequestBody, typeof(RentRequest));
            var libraryResult = await _httpClient.SendAsync(libraryRequest);
            libraryResult.EnsureSuccessStatusCode();

            var library = await _httpClient.GetFromJsonAsync<LibraryResponse>($"http://library:8060/library/{request.libraryUid}");
            var book = await _httpClient.GetFromJsonAsync<BookResponse>($"http://library:8060/book/{request.bookUid}");

            var result = new RentInfoResponse
            {
                reservationUid = reservation.Reservation_uid,
                startDate = reservation.Start_date,
                tillDate = reservation.Till_date,
                status = reservation.Status,
                book = book,
                library = library,
                rating = rating
            };

            return result;
        }

        private async Task<RatingResponse> RatingResponse(string username, string bookUid, DateOnly date, DateOnly tilldate, string condition)
        {
            var book = await _httpClient.GetFromJsonAsync<Books>($"http://library:8060/bookInfo?bookUid={bookUid}");

            var ratingRequestBody = new RatingUpdateRequest
            {
                laterDate = tilldate < date,
                lessCond = condition != book.Condition
            };

            using var ratingUpdateRequest = new HttpRequestMessage(HttpMethod.Post, $"http://rating:8050/rating");
            ratingUpdateRequest.Headers.Add("X-User-Name", username);
            ratingUpdateRequest.Content = JsonContent.Create(ratingRequestBody, typeof(RatingUpdateRequest));
            try
            {
                using var ratingUpdateResult = await _httpClient.SendAsync(ratingUpdateRequest);

                if (!ratingUpdateResult.IsSuccessStatusCode)
                {
                    var reqClone = await HttpRequestMessageHelper.CloneHttpRequestMessageAsync(ratingUpdateRequest);
                    _requestQueueService.AddRequestToQueue(reqClone);
                    return null;
                }

                var response = await ratingUpdateResult.Content.ReadFromJsonAsync<RatingResponse>();
                return response;
            }
            catch (HttpRequestException e)
            {
                var reqClone = await HttpRequestMessageHelper.CloneHttpRequestMessageAsync(ratingUpdateRequest);
                _requestQueueService.AddRequestToQueue(reqClone);
                return null;
            }
        }

        private async Task<string> BookReturn(string bookUid, string libraryUid)
        {
            var libraryRequestBody = new RentRequest { bookUid = bookUid, libraryUid = libraryUid };
            var action = false; //Флаг возврата книги

            var libraryRequest = new HttpRequestMessage(HttpMethod.Post, $"http://library:8060/library/rentBook?action={action}");
            libraryRequest.Content = JsonContent.Create(libraryRequestBody, typeof(RentRequest));
            try
            {
                using var libraryResult = await _httpClient.SendAsync(libraryRequest);
                if (!libraryResult.IsSuccessStatusCode)
                {
                    var reqClone = await HttpRequestMessageHelper.CloneHttpRequestMessageAsync(libraryRequest);
                    _requestQueueService.AddRequestToQueue(reqClone);
                    return null;
                }
                return "true";
            }
            catch (HttpRequestException e)
            {
                var reqClone = await HttpRequestMessageHelper.CloneHttpRequestMessageAsync(libraryRequest);
                _requestQueueService.AddRequestToQueue(reqClone);
                return null;
            }
        }

        public async Task<CloseReservationResponse> CloseBookReservation(string userName, Guid reservationUid, ReturnBookRequest request)
        {
            //Возврат внутри Reservation

            var requestReturnBody = new ReturnRequest { reservationGuid = reservationUid.ToString(), returnDate = request.date };

            using var reserveCloseRequest = new HttpRequestMessage(HttpMethod.Post, $"http://reservation:8070/reservation/return");
            reserveCloseRequest.Content = JsonContent.Create(requestReturnBody, typeof(ReturnRequest));
            using var reserveCloseResult = await _httpClient.SendAsync(reserveCloseRequest);

            if (reserveCloseResult.StatusCode != System.Net.HttpStatusCode.OK)
            {
                return new CloseReservationResponse { isReturned = false };
            }

            var reservation = await reserveCloseResult.Content.ReadFromJsonAsync<ReservationResponse?>();

            if (reservation is null)
            {
                return new CloseReservationResponse { isReturned = false };
            }

            var library = await BookReturn(reservation.Book_uid.ToString(), reservation.Library_uid.ToString());

            var rating = await RatingResponse(userName, reservation.Book_uid.ToString(), request.date, reservation.Till_date, request.condition);

            return new CloseReservationResponse { isReturned = true };
        }

        public async Task<IEnumerable<OpenReservationResponse>> GetBookReservations(string userName)
        {
            if (_circuitBreaker.IsOpened())
            {
                return null;
            }

            var reservationsRequest = new HttpRequestMessage(HttpMethod.Get, $"http://reservation:8070/reservations/get");
            reservationsRequest.Headers.Add("X-User-Name", userName);
            using var reservationsResult = await _httpClient.SendAsync(reservationsRequest);

            var reservationRespones = await reservationsResult.Content.ReadFromJsonAsync<IEnumerable<ReservationResponse>>();


            var responses = new List<OpenReservationResponse>();

            foreach (var reservation in reservationRespones)
            {

                var checkLibrary = await HealthCheckAsync("library:8060");
                LibraryResponse library = null;
                BookResponse book = null;
                if (checkLibrary)
                {
                    library = await _httpClient.GetFromJsonAsync<LibraryResponse>($"http://library:8060/library/{reservation.Library_uid.ToString()}");
                    book = await _httpClient.GetFromJsonAsync<BookResponse>($"http://library:8060/book/{reservation.Book_uid.ToString()}");
                }
                responses.Add(new OpenReservationResponse
                {
                    reservationUid = reservation.Reservation_uid,
                    status = reservation.Status,
                    startDate = reservation.Start_date,
                    tillDate = reservation.Till_date,
                    book = book == null ? reservation.Book_uid : book,
                    library = library == null ? reservation.Library_uid : library
                });
            }
            return responses;
        }
    }
}
