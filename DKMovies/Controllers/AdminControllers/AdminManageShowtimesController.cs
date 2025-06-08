using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DKMovies.Models.ViewModels;
using DKMovies.Models.Data;
using DKMovies.Models.Data.DatabaseModels;

namespace Controllers.Admin
{
    public class AdminManageShowtimesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminManageShowtimesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<JsonResult> GetShowtimeData()
        {
            try
            {
                var nextWeekStart = GetNextWeekStart();
                var nextWeekEnd = nextWeekStart.AddDays(7);

                // Get existing showtimes - ONLY include items with valid IDs
                var existing = await _context.ShowTimes
                    .Include(st => st.Movie)
                    .Include(st => st.Auditorium)
                    .ThenInclude(a => a.Theater)
                    .Where(st => st.StartTime >= nextWeekStart && st.StartTime < nextWeekEnd)
                    .Where(st => st.Movie != null && st.Auditorium != null && st.Auditorium.Theater != null)
                    .Where(st => st.ID > 0)
                    .Select(st => new ShowtimeItem
                    {
                        ShowTimeId = st.ID,
                        MovieId = st.MovieID,
                        MovieTitle = st.Movie.Title,
                        StartTime = st.StartTime,
                        AuditoriumId = st.AuditoriumID,
                        AuditoriumName = st.Auditorium.Name,
                        TheaterName = st.Auditorium.Theater.Name,
                        Price = st.Price,
                        DurationMinutes = st.DurationMinutes
                    })
                    .OrderBy(st => st.StartTime)
                    .ToListAsync();

                var validExisting = existing.Where(e => e.ShowTimeId.HasValue && e.ShowTimeId.Value > 0).ToList();

                // Generate suggestions
                var suggested = await GenerateSuggestions(nextWeekStart, validExisting);

                return Json(new
                {
                    success = true,
                    suggested = suggested,
                    existing = validExisting
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<JsonResult> AddShowtimes([FromBody] ShowtimeActionRequest request)
        {
            try
            {
                var added = 0;
                foreach (var item in request.Showtimes ?? new List<ShowtimeItem>())
                {
                    if (!await _context.Movies.AnyAsync(m => m.ID == item.MovieId) ||
                        !await _context.Auditoriums.AnyAsync(a => a.ID == item.AuditoriumId))
                        continue;

                    // Check for conflicts - same auditorium at same time
                    var hasConflict = await _context.ShowTimes.AnyAsync(st =>
                        st.AuditoriumID == item.AuditoriumId &&
                        st.StartTime < item.StartTime.AddMinutes(item.DurationMinutes + 30) &&
                        st.StartTime.AddMinutes(st.DurationMinutes + 30) > item.StartTime);

                    if (hasConflict) continue;

                    var showtime = new ShowTime
                    {
                        MovieID = item.MovieId,
                        AuditoriumID = item.AuditoriumId,
                        StartTime = item.StartTime,
                        DurationMinutes = item.DurationMinutes,
                        Price = item.Price,
                        SubtitleLanguageID = 1,
                        Is3D = false
                    };

                    _context.ShowTimes.Add(showtime);
                    added++;
                }

                await _context.SaveChangesAsync();
                return Json(new { success = true, addedCount = added });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<JsonResult> RemoveShowtimes([FromBody] ShowtimeActionRequest request)
        {
            try
            {
                if (request.Showtimes == null || !request.Showtimes.Any())
                {
                    return Json(new { success = false, error = "No showtimes provided" });
                }

                var ids = request.Showtimes
                    .Where(s => s.ShowTimeId.HasValue)
                    .Select(s => s.ShowTimeId.Value)
                    .ToList();

                if (!ids.Any())
                {
                    return Json(new { success = false, error = "No valid showtime IDs found in request" });
                }

                var toRemove = await _context.ShowTimes
                    .Where(st => ids.Contains(st.ID))
                    .ToListAsync();

                _context.ShowTimes.RemoveRange(toRemove);
                await _context.SaveChangesAsync();

                return Json(new { success = true, removedCount = toRemove.Count });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        private async Task<List<ShowtimeItem>> GenerateSuggestions(DateTime weekStart, List<ShowtimeItem> existing)
        {
            var suggestions = new List<ShowtimeItem>();
            var threeMonthsAgo = DateTime.Now.AddMonths(-3);

            // Get top theaters by revenue in last 3 months
            var theaterRevenues = await _context.Theaters
                .Include(t => t.Auditoriums)
                .Where(t => t.Auditoriums.Any())
                .Select(t => new
                {
                    Theater = t,
                    TicketRevenue = _context.Tickets
                        .Where(ticket => ticket.ShowTime.Auditorium.TheaterID == t.ID &&
                                       ticket.ShowTime.StartTime >= threeMonthsAgo &&
                                       (ticket.Status == TicketStatus.PAID || ticket.Status == TicketStatus.CONFIRMED))
                        .Sum(ticket => ticket.TicketSeats.Count() * ticket.ShowTime.Price),
                    ConcessionRevenue = _context.OrderItems
                        .Where(oi => oi.Ticket.ShowTime.Auditorium.TheaterID == t.ID &&
                                   oi.Ticket.ShowTime.StartTime >= threeMonthsAgo &&
                                   (oi.Ticket.Status == TicketStatus.PAID || oi.Ticket.Status == TicketStatus.CONFIRMED))
                        .Sum(oi => oi.PriceAtPurchase * oi.Quantity)
                })
                .ToListAsync();

            var topTheaters = theaterRevenues
                .OrderByDescending(t => t.TicketRevenue + t.ConcessionRevenue)
                .Take(3)
                .Select(t => t.Theater)
                .ToList();

            // Get top movies by revenue and high-rated movies
            var movieRevenues = await _context.Movies
                .Where(m => m.ReleaseDate <= DateTime.Now)
                .Select(m => new
                {
                    Movie = new { m.ID, m.Title, m.DurationMinutes },
                    TicketRevenue = _context.Tickets
                        .Where(ticket => ticket.ShowTime.MovieID == m.ID &&
                                       ticket.ShowTime.StartTime >= threeMonthsAgo &&
                                       (ticket.Status == TicketStatus.PAID || ticket.Status == TicketStatus.CONFIRMED))
                        .Sum(ticket => ticket.TicketSeats.Count() * ticket.ShowTime.Price)
                })
                .ToListAsync();

            var topRevenueMovies = movieRevenues
                .OrderByDescending(m => m.TicketRevenue)
                .Take(3)
                .Select(m => m.Movie)
                .ToList();

            var highRatedMovies = await _context.Movies
                .Where(m => m.ReleaseDate <= DateTime.Now)
                .Where(m => m.Reviews.Any(r => r.IsApproved))
                .Select(m => new
                {
                    ID = m.ID,
                    Title = m.Title,
                    DurationMinutes = m.DurationMinutes,
                    AvgRating = m.Reviews.Where(r => r.IsApproved).Average(r => r.Rating)
                })
                .Where(m => m.AvgRating >= 4.0)
                .OrderByDescending(m => m.AvgRating)
                .Take(3)
                .Select(m => new { m.ID, m.Title, m.DurationMinutes })
                .ToListAsync();

            var movies = topRevenueMovies.Union(highRatedMovies).Distinct().Take(5).ToList();

            if (!movies.Any() || !topTheaters.Any()) return suggestions;

            // Time slots from 8 AM to 8 PM (20:00)
            var timeSlots = new[] { 8, 10, 12, 14, 16, 18, 20 };

            foreach (var movie in movies)
            {
                for (int day = 0; day < 7; day++)
                {
                    var showDate = weekStart.AddDays(day);
                    if (showDate < DateTime.Now.Date) continue;

                    foreach (var theater in topTheaters)
                    {
                        // Maximum 10 showtimes per theater per day
                        var dailyCount = existing.Count(e =>
                            e.TheaterName == theater.Name &&
                            e.StartTime.Date == showDate);

                        if (dailyCount >= 10) continue;

                        foreach (var hour in timeSlots)
                        {
                            var startTime = new DateTime(showDate.Year, showDate.Month, showDate.Day, hour, 0, 0);

                            // Find available auditorium - no conflicts
                            var availableAud = theater.Auditoriums.FirstOrDefault(aud =>
                                !existing.Any(e => e.AuditoriumId == aud.ID &&
                                    e.StartTime < startTime.AddMinutes(movie.DurationMinutes + 30) &&
                                    e.StartTime.AddMinutes(e.DurationMinutes + 30) > startTime));

                            if (availableAud != null)
                            {
                                var suggestion = new ShowtimeItem
                                {
                                    MovieId = movie.ID,
                                    MovieTitle = movie.Title,
                                    StartTime = startTime,
                                    AuditoriumId = availableAud.ID,
                                    AuditoriumName = availableAud.Name,
                                    TheaterName = theater.Name,
                                    Price = 12.00m,
                                    DurationMinutes = movie.DurationMinutes
                                };

                                suggestions.Add(suggestion);
                                existing.Add(suggestion);
                                break;
                            }
                        }
                    }
                }
            }

            return suggestions.OrderBy(s => s.StartTime).ToList();
        }

        private DateTime GetNextWeekStart()
        {
            var today = DateTime.Now.Date;
            var daysUntilMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
            if (daysUntilMonday == 0) daysUntilMonday = 7;
            return today.AddDays(daysUntilMonday);
        }

        public class ShowtimeActionRequest
        {
            public List<ShowtimeItem> Showtimes { get; set; } = new List<ShowtimeItem>();
        }

        public class ShowtimeItem
        {
            public int? ShowTimeId { get; set; }
            public int MovieId { get; set; }
            public string MovieTitle { get; set; }
            public DateTime StartTime { get; set; }
            public int AuditoriumId { get; set; }
            public string AuditoriumName { get; set; }
            public string TheaterName { get; set; }
            public decimal Price { get; set; }
            public int DurationMinutes { get; set; }
        }
    }
}