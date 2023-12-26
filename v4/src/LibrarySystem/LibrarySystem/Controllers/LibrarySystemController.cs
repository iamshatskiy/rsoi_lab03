using LibrarySystem.DTO;
using LibrarySystem.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace LibrarySystem.Controllers
{
    [ApiController]
    [Route("api/v1/")]
    public class LibrarySystemController : Controller
    {
        readonly ILibrarySystemService _librarySystemService;

        public LibrarySystemController(ILibrarySystemService librarySystemService)
        {
            _librarySystemService = librarySystemService;
        }

        //DONE
        [HttpGet("libraries")]
        public async Task<ActionResult<PaginationResponse<LibraryResponse>>> GetCityLibraries([FromQuery, Required] string city, [FromQuery] int? page, [FromQuery] int? size)
        {
            var checkLibraryState = _librarySystemService.HealthCheckAsync("library:8060");

            if (!await checkLibraryState)
            {
                var resp = new ErrorResponse()
                {
                    Message = "Library Service unavailable",
                };

                Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return new ObjectResult(resp);
            }
            var availableBooks = await _librarySystemService.GetCityLibraries(page, size, city);

            return availableBooks;
        }

        //DONE
        [HttpGet("libraries/{libraryUid}/books")]
        public async Task<ActionResult<PaginationResponse<LibraryBookResponse>>> GetLibraryBooks([FromRoute] string libraryUid, [FromQuery] int? page, [FromQuery] int? size, [FromQuery] bool allShow = false)
        {
            var checkLibraryState = _librarySystemService.HealthCheckAsync("library:8060");

            if (!(await checkLibraryState))
            {
                var resp = new ErrorResponse()
                {
                    Message = "Library Service unavailable",
                };

                Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return new ObjectResult(resp);
            }
            var books = await _librarySystemService.GetLibraryBooks(page, size, Guid.Parse(libraryUid), allShow);

            return books;
        }

        [HttpGet("reservations")]
        public async Task<ActionResult<IEnumerable<OpenReservationResponse>>> GetBookReservations([FromHeader(Name = "X-User-Name"), Required] string xUserName)
        {
            if (string.IsNullOrWhiteSpace(xUserName))
            {
                return BadRequest();
            }

            var checkReservaionState = _librarySystemService.HealthCheckAsync("reservation:8070");

            if (!(await checkReservaionState))
            {
                var resp = new ErrorResponse()
                {
                    Message = "Reservation Service unavailable",
                };

                Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return new ObjectResult(resp);
            }

            var reservations = await _librarySystemService.GetBookReservations(xUserName);

            if (reservations == null || !reservations.Any())
            {
                return null;
            }

            return Ok(reservations);
        }


        //DONE
        [HttpPost("reservations")]
        public async Task<ActionResult<RentInfoResponse>> CreateBookReservation([FromHeader(Name = "X-User-Name"), Required] string xUserName, [FromBody] RentingRequest request)
        {
            if (string.IsNullOrWhiteSpace(xUserName))
            {
                return BadRequest();
            }

            var reservation = await _librarySystemService.CreateBookReservation(xUserName, request);
            if (reservation is string)
            {
                var resp = new ErrorResponse()
                {
                    Message = reservation.ToString()
                };

                Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return new ObjectResult(resp);
            }
            if (reservation == null)
            {
                return null;
            }
            return (RentInfoResponse)reservation;
        }


        //DONE
        [HttpPost("reservations/{reservationUid}/return")]
        public async Task<ActionResult<CloseReservationResponse>> CloseBookReservation([FromRoute, Required] string reservationUid, [FromHeader(Name = "X-User-Name"), Required] string xUserName, [FromBody] ReturnBookRequest request)
        {

            var checkReservaionState = _librarySystemService.HealthCheckAsync("reservation:8070");

            if (!(await checkReservaionState))
            {
                var resp = new ErrorResponse()
                {
                    Message = "Reservation Service unavailable",
                };

                Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return new ObjectResult(resp);
            }

            string[] validConditions = { "EXCELLENT", "GOOD", "BAD" };
            if (string.IsNullOrWhiteSpace(xUserName) || !(Array.Exists(validConditions, element => element == request.condition)))
            {
                return BadRequest();
            }

            var returnCond = await _librarySystemService.CloseBookReservation(xUserName, Guid.Parse(reservationUid), request);
            if (returnCond.isReturned == false)
            {
                return NotFound($"User {xUserName} has no open reservation with UUID = {reservationUid}");
            }
            return NoContent();
        }

        [HttpGet("rating")]
        public async Task<ActionResult<RatingResponse?>> GetUserRating([FromHeader(Name = "X-User-Name"), Required] string xUserName)
        {
            if (string.IsNullOrWhiteSpace(xUserName))
            {
                return BadRequest();

            }

            var checkRatingState = _librarySystemService.HealthCheckAsync("rating:8050");

            if (!(await checkRatingState))
            {
                var resp = new ErrorResponse()
                {
                    Message = "Bonus Service unavailable",
                };

                Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return new ObjectResult(resp);
            }

            var rating = await _librarySystemService.GetRatingResponseByUserName(xUserName);
            return rating;
        }

    }
}