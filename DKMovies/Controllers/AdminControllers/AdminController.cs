using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DKMovies.Models.ViewModels;
using DKMovies.Models.Data;
using DKMovies.Models.Data.DatabaseModels;
using Microsoft.Data.SqlClient;

namespace Controllers.Admin
{
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Admins Dashboard
        public async Task<IActionResult> Index(string period = "all")
        {
            try
            {
                // Define date filter with proper end date
                DateTime? startDate = null;
                DateTime? endDate = DateTime.Now; // Always filter up to now

                switch (period)
                {
                    case "7days":
                        startDate = DateTime.Now.AddDays(-7);
                        break;
                    case "month":
                        startDate = DateTime.Now.AddMonths(-1);
                        break;
                    case "year":
                        startDate = DateTime.Now.AddYears(-1);
                        break;
                    default: // "all"
                        startDate = null;
                        endDate = null;
                        break;
                }

                // Get basic counts - these should probably be filtered by date too if needed
                var totalUsers = await _context.Users.CountAsync();
                var totalEmployees = await _context.Employees.CountAsync();
                var totalMovies = await _context.Movies.CountAsync();
                var totalShowTimes = await _context.ShowTimes.CountAsync();
                var totalConcessions = await _context.Concessions.CountAsync();

                // Get revenue data with proper date filtering
                var ticketQuery = _context.Tickets
                    .Where(t => t.Status == TicketStatus.PAID || t.Status == TicketStatus.CONFIRMED);

                // Apply date filter consistently
                if (startDate.HasValue)
                {
                    ticketQuery = ticketQuery.Where(t => t.PurchaseTime >= startDate.Value && t.PurchaseTime <= endDate.Value);
                }

                var ticketRevenue = await ticketQuery
                    .Join(_context.TicketSeats, t => t.ID, ts => ts.TicketID, (t, ts) => new { t, ts })
                    .Join(_context.ShowTimes, x => x.t.ShowTimeID, st => st.ID, (x, st) => st.Price)
                    .SumAsync(price => price);

                var concessionRevenue = await _context.OrderItems
                    .Join(_context.Tickets, oi => oi.TicketID, t => t.ID, (oi, t) => new { oi, t })
                    .Where(x => (x.t.Status == TicketStatus.PAID || x.t.Status == TicketStatus.CONFIRMED))
                    .Where(x => !startDate.HasValue || (x.t.PurchaseTime >= startDate.Value && x.t.PurchaseTime <= endDate.Value))
                    .SumAsync(x => x.oi.Quantity * x.oi.PriceAtPurchase);

                var totalRevenue = ticketRevenue + concessionRevenue;

                // Get top movies with consistent date filtering
                var movieRevenueData = await _context.Movies
                    .Select(m => new
                    {
                        Movie = m,
                        Revenue = m.ShowTimes
                            .SelectMany(st => st.Tickets
                                .Where(t => (t.Status == TicketStatus.PAID || t.Status == TicketStatus.CONFIRMED) &&
                                           (!startDate.HasValue || (t.PurchaseTime >= startDate.Value && t.PurchaseTime <= endDate.Value))))
                            .SelectMany(t => t.TicketSeats)
                            .Sum(ts => (decimal?)ts.Ticket.ShowTime.Price) ?? 0,
                        TicketsSold = m.ShowTimes
                            .SelectMany(st => st.Tickets
                                .Where(t => (t.Status == TicketStatus.PAID || t.Status == TicketStatus.CONFIRMED) &&
                                           (!startDate.HasValue || (t.PurchaseTime >= startDate.Value && t.PurchaseTime <= endDate.Value))))
                            .SelectMany(t => t.TicketSeats)
                            .Count(),
                        ShowTimesCount = m.ShowTimes.Count(),
                        AverageRating = m.Reviews.Any() ? m.Reviews.Average(r => r.Rating) : 0,
                        TotalReviews = m.Reviews.Count()
                    })
                    .Where(x => x.Revenue > 0)
                    .OrderByDescending(x => x.Revenue)
                    .Take(5)
                    .ToListAsync();

                // Get genres for top movies
                var topMovieIDs = movieRevenueData.Select(m => m.Movie.ID).ToList();
                var movieGenres = await _context.MovieGenres
                    .Where(mg => topMovieIDs.Contains(mg.MovieID))
                    .Include(mg => mg.Genre)
                    .ToListAsync();

                // Get top theaters with consistent date filtering
                var theaterRevenueData = await _context.Theaters
                    .Select(th => new
                    {
                        Theater = th,
                        TicketRevenue = th.Auditoriums
                            .SelectMany(a => a.ShowTimes)
                            .SelectMany(st => st.Tickets
                                .Where(t => (t.Status == TicketStatus.PAID || t.Status == TicketStatus.CONFIRMED) &&
                                           (!startDate.HasValue || (t.PurchaseTime >= startDate.Value && t.PurchaseTime <= endDate.Value))))
                            .SelectMany(t => t.TicketSeats)
                            .Sum(ts => (decimal?)ts.Ticket.ShowTime.Price) ?? 0,
                        ConcessionRevenue = th.TheaterConcessions
                            .SelectMany(tc => tc.OrderItems)
                            .Where(oi => (oi.Ticket.Status == TicketStatus.PAID || oi.Ticket.Status == TicketStatus.CONFIRMED) &&
                                        (!startDate.HasValue || (oi.Ticket.PurchaseTime >= startDate.Value && oi.Ticket.PurchaseTime <= endDate.Value)))
                            .Sum(oi => (decimal?)(oi.Quantity * oi.PriceAtPurchase)) ?? 0,
                        TotalTicketsSold = th.Auditoriums
                            .SelectMany(a => a.ShowTimes)
                            .SelectMany(st => st.Tickets
                                .Where(t => (t.Status == TicketStatus.PAID || t.Status == TicketStatus.CONFIRMED) &&
                                           (!startDate.HasValue || (t.PurchaseTime >= startDate.Value && t.PurchaseTime <= endDate.Value))))
                            .SelectMany(t => t.TicketSeats)
                            .Count(),
                        TotalAuditoriums = th.Auditoriums.Count(),
                        TotalCapacity = th.Auditoriums.SelectMany(a => a.Seats).Count()
                    })
                    .Where(x => x.TicketRevenue + x.ConcessionRevenue > 0)
                    .OrderByDescending(x => x.TicketRevenue + x.ConcessionRevenue)
                    .Take(5)
                    .ToListAsync();

                // Get theater images for top theaters
                var topTheaterIDs = theaterRevenueData.Select(t => t.Theater.ID).ToList();
                var theaterImages = await _context.TheaterImages
                    .Where(ti => topTheaterIDs.Contains(ti.TheaterID))
                    .ToListAsync();

                // Get top concessions with consistent date filtering
                var concessionRevenueData = await _context.Concessions
                    .Select(c => new
                    {
                        Concession = c,
                        Revenue = c.TheaterConcessions
                            .SelectMany(tc => tc.OrderItems)
                            .Where(oi => (oi.Ticket.Status == TicketStatus.PAID || oi.Ticket.Status == TicketStatus.CONFIRMED) &&
                                        (!startDate.HasValue || (oi.Ticket.PurchaseTime >= startDate.Value && oi.Ticket.PurchaseTime <= endDate.Value)))
                            .Sum(oi => (decimal?)(oi.Quantity * oi.PriceAtPurchase)) ?? 0,
                        TotalQuantitySold = c.TheaterConcessions
                            .SelectMany(tc => tc.OrderItems)
                            .Where(oi => (oi.Ticket.Status == TicketStatus.PAID || oi.Ticket.Status == TicketStatus.CONFIRMED) &&
                                        (!startDate.HasValue || (oi.Ticket.PurchaseTime >= startDate.Value && oi.Ticket.PurchaseTime <= endDate.Value)))
                            .Sum(oi => oi.Quantity),
                        AveragePrice = c.TheaterConcessions
                            .SelectMany(tc => tc.OrderItems)
                            .Where(oi => (oi.Ticket.Status == TicketStatus.PAID || oi.Ticket.Status == TicketStatus.CONFIRMED) &&
                                        (!startDate.HasValue || (oi.Ticket.PurchaseTime >= startDate.Value && oi.Ticket.PurchaseTime <= endDate.Value)))
                            .Average(oi => (decimal?)oi.PriceAtPurchase) ?? 0,
                        TheaterCount = c.TheaterConcessions.Count()
                    })
                    .Where(x => x.Revenue > 0)
                    .OrderByDescending(x => x.Revenue)
                    .Take(5)
                    .ToListAsync();

                var model = new DashboardViewModel
                {
                    TotalUsers = totalUsers,
                    TotalEmployees = totalEmployees,
                    TotalMovies = totalMovies,
                    TotalShowTimes = totalShowTimes,
                    TotalConcessions = totalConcessions,
                    TotalRevenue = totalRevenue,
                    TicketRevenue = ticketRevenue,
                    ConcessionRevenue = concessionRevenue,
                    CurrentPeriod = period,
                    TopMovies = movieRevenueData.Select(tm => new DKMovies.Models.ViewModels.TopMovieViewModel
                    {
                        MovieTitle = tm.Movie.Title,
                        Revenue = tm.Revenue,
                        PosterImagePath = tm.Movie.PosterImagePath,
                        TicketsSold = tm.TicketsSold,
                        PriorityScore = tm.Revenue,
                        ShowTimesCount = tm.ShowTimesCount,
                        AverageRating = tm.AverageRating,
                        TotalReviews = tm.TotalReviews,
                        Genre = string.Join(", ", movieGenres.Where(mg => mg.MovieID == tm.Movie.ID).Select(mg => mg.Genre.Name)),
                        Duration = tm.Movie.DurationMinutes
                    }).ToList(),
                    TopTheaters = theaterRevenueData.Select(tt => new DKMovies.Models.ViewModels.TopTheaterViewModel
                    {
                        TheaterName = tt.Theater.Name,
                        Location = tt.Theater.Location,
                        TicketRevenue = tt.TicketRevenue,
                        ConcessionRevenue = tt.ConcessionRevenue,
                        TotalRevenue = tt.TicketRevenue + tt.ConcessionRevenue,
                        TotalTicketsSold = tt.TotalTicketsSold,
                        TotalAuditoriums = tt.TotalAuditoriums,
                        TotalCapacity = tt.TotalCapacity,
                        OccupancyRate = tt.TotalCapacity > 0 ? (double)tt.TotalTicketsSold / tt.TotalCapacity * 100 : 0,
                        TheaterImages = theaterImages
                            .Where(ti => ti.TheaterID == tt.Theater.ID)
                            .Select(ti => new DKMovies.Models.ViewModels.TheaterImageViewModel { ImageUrl = ti.ImageUrl })
                            .ToList()
                    }).ToList(),
                    TopConcessions = concessionRevenueData.Select(tc => new DKMovies.Models.ViewModels.TopConcessionViewModel
                    {
                        ConcessionName = tc.Concession.Name,
                        Revenue = tc.Revenue,
                        QuantitySold = tc.TotalQuantitySold,
                        ImagePath = tc.Concession.ImagePath,
                        AveragePrice = tc.AveragePrice,
                        TheaterCount = tc.TheaterCount,
                    }).ToList()
                };

                // ViewBag for backward compatibility
                ViewBag.TotalUsers = totalUsers;
                ViewBag.TotalEmployees = totalEmployees;
                ViewBag.TotalMovies = totalMovies;
                ViewBag.TotalShowTimes = totalShowTimes;
                ViewBag.TotalConcessions = totalConcessions;
                ViewBag.TotalRevenue = totalRevenue;
                ViewBag.TicketRevenue = ticketRevenue;
                ViewBag.ConcessionRevenue = concessionRevenue;

                return View(model);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Dashboard error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");

                ViewBag.TotalUsers = 0;
                ViewBag.TotalEmployees = 0;
                ViewBag.TotalMovies = 0;
                ViewBag.TotalShowTimes = 0;
                ViewBag.TotalConcessions = 0;
                ViewBag.TotalRevenue = 0;
                ViewBag.TicketRevenue = 0;
                ViewBag.ConcessionRevenue = 0;
                ViewBag.ErrorMessage = "An error occurred while loading dashboard data.";

                return View(new DashboardViewModel
                {
                    TopMovies = new List<DKMovies.Models.ViewModels.TopMovieViewModel>(),
                    TopTheaters = new List<DKMovies.Models.ViewModels.TopTheaterViewModel>(),
                    TopConcessions = new List<DKMovies.Models.ViewModels.TopConcessionViewModel>(),
                    CurrentPeriod = period
                });
            }
        }
    }
}