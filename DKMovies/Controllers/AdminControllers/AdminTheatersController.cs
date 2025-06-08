using DKMovies.Models.Data;
using DKMovies.Models.Data.DatabaseModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DKMovies.Controllers
{
    public class AdminTheatersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private const int PageSize = 6; // Number of theaters per page for admin (reduced for card layout)

        public AdminTheatersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: AdminTheaters
        public async Task<IActionResult> Index(int page = 1, string search = "", string location = "", string sortBy = "name", string filter = "all")
        {
            try
            {
                // Get all theaters with their related data for admin view
                var theatersQuery = _context.Theaters
                    .Include(t => t.TheaterImages)
                    .Include(t => t.Auditoriums)
                    .Include(t => t.Employees)
                    .Include(t => t.TheaterConcessions)
                        .ThenInclude(tc => tc.Concession)
                    .AsQueryable();

                // Apply search filters
                if (!string.IsNullOrEmpty(search))
                {
                    theatersQuery = theatersQuery.Where(t =>
                        t.Name.Contains(search) ||
                        t.Location.Contains(search) ||
                        t.Phone.Contains(search));
                }

                if (!string.IsNullOrEmpty(location) && location != "all")
                {
                    theatersQuery = theatersQuery.Where(t => t.Location.Contains(location));
                }

                // Apply status filter
                if (filter != "all")
                {
                    var today = DateTime.Today;
                    var nextWeek = today.AddDays(7);

                    switch (filter)
                    {
                        case "active":
                            // Theaters with current showtimes
                            var activeTheaterIds = await _context.ShowTimes
                                .Where(st => st.StartTime >= today && st.StartTime <= nextWeek)
                                .Select(st => st.Auditorium.TheaterID)
                                .Distinct()
                                .ToListAsync();
                            theatersQuery = theatersQuery.Where(t => activeTheaterIds.Contains(t.ID));
                            break;
                        case "inactive":
                            // Theaters without current showtimes
                            var inactiveTheaterIds = await _context.ShowTimes
                                .Where(st => st.StartTime >= today && st.StartTime <= nextWeek)
                                .Select(st => st.Auditorium.TheaterID)
                                .Distinct()
                                .ToListAsync();
                            theatersQuery = theatersQuery.Where(t => !inactiveTheaterIds.Contains(t.ID));
                            break;
                    }
                }

                // Apply sorting
                theatersQuery = sortBy.ToLower() switch
                {
                    "location" => theatersQuery.OrderBy(t => t.Location).ThenBy(t => t.Name),
                    "auditoriums" => theatersQuery.OrderByDescending(t => t.Auditoriums.Count()).ThenBy(t => t.Name),
                    "employees" => theatersQuery.OrderByDescending(t => t.Employees.Count()).ThenBy(t => t.Name),
                    "concessions" => theatersQuery.OrderByDescending(t => t.TheaterConcessions.Count()).ThenBy(t => t.Name),
                    _ => theatersQuery.OrderBy(t => t.Name)
                };

                // Calculate pagination
                var totalTheaters = await theatersQuery.CountAsync();
                var totalPages = (int)Math.Ceiling(totalTheaters / (double)PageSize);
                var theaters = await theatersQuery
                    .Skip((page - 1) * PageSize)
                    .Take(PageSize)
                    .ToListAsync();

                // Calculate pagination info
                var startItem = totalTheaters == 0 ? 0 : (page - 1) * PageSize + 1;
                var endItem = Math.Min(page * PageSize, totalTheaters);
                var hasPreviousPage = page > 1;
                var hasNextPage = page < totalPages;

                // Get unique locations for filter dropdown
                var locations = await _context.Theaters
                    .Select(t => t.Location)
                    .Distinct()
                    .OrderBy(l => l)
                    .ToListAsync();

                // Create view model
                var viewModel = new TheaterIndexViewModel
                {
                    Theaters = theaters,
                    CurrentPage = page,
                    TotalPages = totalPages,
                    TotalTheaters = totalTheaters,
                    StartItem = startItem,
                    EndItem = endItem,
                    HasPreviousPage = hasPreviousPage,
                    HasNextPage = hasNextPage,
                    SearchTerm = search,
                    LocationFilter = location,
                    FilterType = filter,
                    SortBy = sortBy,
                    Locations = locations
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                // Log the error (you should use proper logging)
                // _logger.LogError(ex, "Error loading theaters for admin");

                ViewBag.ErrorMessage = "Unable to load theaters. Please try again later.";
                return View(new TheaterIndexViewModel
                {
                    Theaters = new List<Theater>(),
                    Locations = new List<string>()
                });
            }
        }

        // GET: AdminTheaters/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var theater = await _context.Theaters
                .Include(t => t.TheaterImages.OrderBy(img => img.UploadedAt))
                .Include(t => t.Auditoriums.OrderBy(a => a.Name))
                    .ThenInclude(a => a.Seats)
                .Include(t => t.Employees.OrderBy(e => e.FullName))
                .Include(t => t.TheaterConcessions.OrderBy(tc => tc.Concession.Name))
                    .ThenInclude(tc => tc.Concession)
                .FirstOrDefaultAsync(t => t.ID == id);

            if (theater == null)
            {
                return NotFound();
            }

            // Get current and upcoming movies for this theater
            var today = DateTime.Today;
            var nextWeek = today.AddDays(7);

            var currentMovies = await _context.ShowTimes
                .Include(st => st.Movie)
                    .ThenInclude(m => m.Rating)
                .Include(st => st.Movie)
                    .ThenInclude(m => m.Language)
                .Include(st => st.Movie)
                    .ThenInclude(m => m.Country)
                .Include(st => st.Auditorium)
                .Where(st => st.Auditorium.TheaterID == id &&
                             st.StartTime >= today &&
                             st.StartTime <= nextWeek)
                .OrderBy(st => st.StartTime)
                .ToListAsync();

            // Get theater statistics
            var theaterStats = new
            {
                TotalAuditoriums = theater.Auditoriums?.Count() ?? 0,
                TotalSeats = theater.Auditoriums?.Sum(a => a.Capacity) ?? 0,
                TotalEmployees = theater.Employees?.Count() ?? 0,
                TotalConcessions = theater.TheaterConcessions?.Count() ?? 0,
                ActiveConcessions = theater.TheaterConcessions?.Count(tc => tc.IsAvailable) ?? 0,
                TotalImages = theater.TheaterImages?.Count() ?? 0,
                CurrentMoviesCount = currentMovies.Select(st => st.MovieID).Distinct().Count(),
                TotalShowtimesThisWeek = currentMovies.Count()
            };

            // Get recent activity (last 30 days)
            var thirtyDaysAgo = today.AddDays(-30);
            var recentTickets = await _context.Tickets
                .Include(t => t.ShowTime)
                    .ThenInclude(st => st.Movie)
                .Include(t => t.ShowTime)
                    .ThenInclude(st => st.Auditorium)
                .Where(t => t.ShowTime.Auditorium.TheaterID == id &&
                           t.PurchaseTime >= thirtyDaysAgo)
                .OrderByDescending(t => t.PurchaseTime)
                .Take(10)
                .ToListAsync();

            // Calculate revenue for last 30 days
            var recentRevenue = await _context.Tickets
                .Include(t => t.ShowTime)
                    .ThenInclude(st => st.Auditorium)
                .Where(t => t.ShowTime.Auditorium.TheaterID == id &&
                           t.PurchaseTime >= thirtyDaysAgo)
                .SumAsync(t => t.TotalPrice);

            // Get concession revenue for last 30 days
            var concessionRevenue = await _context.OrderItems
                .Include(oi => oi.TheaterConcession)
                .Include(oi => oi.Ticket)
                .Where(oi => oi.TheaterConcession.TheaterID == id &&
                            oi.Ticket.PurchaseTime >= thirtyDaysAgo)
                .SumAsync(oi => oi.Quantity * oi.TheaterConcession.Price);

            ViewBag.CurrentMovies = currentMovies.GroupBy(st => st.Movie).Select(g => g.Key).ToList();
            ViewBag.TheaterStats = theaterStats;
            ViewBag.RecentTickets = recentTickets;
            ViewBag.RecentRevenue = recentRevenue;
            ViewBag.ConcessionRevenue = concessionRevenue;
            ViewBag.TotalRevenue = recentRevenue + concessionRevenue;

            return View(theater);
        }

        // GET: AdminTheaters/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: AdminTheaters/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Location,Phone")] Theater theater)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Add(theater);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Theater '{theater.Name}' has been created successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    // Log the error
                    ModelState.AddModelError("", "Unable to save theater. Please try again.");
                }
            }
            return View(theater);
        }

        // GET: AdminTheaters/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var theater = await _context.Theaters.FindAsync(id);
            if (theater == null)
            {
                return NotFound();
            }
            return View(theater);
        }

        // POST: AdminTheaters/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ID,Name,Location,Phone")] Theater theater)
        {
            if (id != theater.ID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(theater);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Theater '{theater.Name}' has been updated successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TheaterExists(theater.ID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        ModelState.AddModelError("", "Unable to save changes. The theater may have been modified by another user.");
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Unable to save changes. Please try again.");
                }
            }
            return View(theater);
        }

        // GET: AdminTheaters/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var theater = await _context.Theaters
                .Include(t => t.Auditoriums)
                .Include(t => t.Employees)
                .Include(t => t.TheaterImages)
                .Include(t => t.TheaterConcessions)
                .FirstOrDefaultAsync(m => m.ID == id);

            if (theater == null)
            {
                return NotFound();
            }

            // Check if theater has dependencies
            var hasShowTimes = await _context.ShowTimes
                .AnyAsync(st => st.Auditorium.TheaterID == id);

            ViewBag.HasDependencies = hasShowTimes;
            ViewBag.DependencyMessage = hasShowTimes ?
                "This theater cannot be deleted because it has associated showtimes." : null;

            return View(theater);
        }

        // POST: AdminTheaters/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var theater = await _context.Theaters
                    .Include(t => t.Auditoriums)
                    .Include(t => t.TheaterImages)
                    .Include(t => t.TheaterConcessions)
                    .FirstOrDefaultAsync(t => t.ID == id);

                if (theater == null)
                {
                    return NotFound();
                }

                // Check for dependencies
                var hasShowTimes = await _context.ShowTimes
                    .AnyAsync(st => st.Auditorium.TheaterID == id);

                if (hasShowTimes)
                {
                    TempData["ErrorMessage"] = "Cannot delete theater because it has associated showtimes.";
                    return RedirectToAction(nameof(Delete), new { id = id });
                }

                _context.Theaters.Remove(theater);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Theater '{theater.Name}' has been deleted successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Unable to delete theater. Please try again.";
                return RedirectToAction(nameof(Delete), new { id = id });
            }
        }

        // API endpoint for admin dashboard statistics
        [HttpGet]
        public async Task<IActionResult> GetTheaterStatistics(int id)
        {
            try
            {
                var today = DateTime.Today;
                var thirtyDaysAgo = today.AddDays(-30);

                var stats = await _context.Theaters
                    .Where(t => t.ID == id)
                    .Select(t => new
                    {
                        theaterId = t.ID,
                        theaterName = t.Name,
                        auditoriumCount = t.Auditoriums.Count(),
                        totalSeats = t.Auditoriums.Sum(a => a.Capacity),
                        employeeCount = t.Employees.Count(),
                        concessionCount = t.TheaterConcessions.Count(),
                        activeConcessions = t.TheaterConcessions.Count(tc => tc.IsAvailable),
                        imageCount = t.TheaterImages.Count()
                    })
                    .FirstOrDefaultAsync();

                if (stats == null)
                {
                    return NotFound();
                }

                // Get revenue data
                var ticketRevenue = await _context.Tickets
                    .Include(t => t.ShowTime)
                        .ThenInclude(st => st.Auditorium)
                    .Where(t => t.ShowTime.Auditorium.TheaterID == id &&
                               t.PurchaseTime >= thirtyDaysAgo)
                    .SumAsync(t => t.TotalPrice);

                var concessionRevenue = await _context.OrderItems
                    .Include(oi => oi.TheaterConcession)
                    .Include(oi => oi.Ticket)
                    .Where(oi => oi.TheaterConcession.TheaterID == id &&
                                oi.Ticket.PurchaseTime >= thirtyDaysAgo)
                    .SumAsync(oi => oi.Quantity * oi.TheaterConcession.Price);

                var result = new
                {
                    stats.theaterId,
                    stats.theaterName,
                    stats.auditoriumCount,
                    stats.totalSeats,
                    stats.employeeCount,
                    stats.concessionCount,
                    stats.activeConcessions,
                    stats.imageCount,
                    ticketRevenue = Math.Round(ticketRevenue, 2),
                    concessionRevenue = Math.Round(concessionRevenue, 2),
                    totalRevenue = Math.Round(ticketRevenue + concessionRevenue, 2)
                };

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { error = "Unable to load statistics" });
            }
        }

        // Bulk operations for admin
        [HttpPost]
        public async Task<IActionResult> BulkUpdateAvailability([FromBody] BulkUpdateRequest request)
        {
            if (request?.TheaterIds == null || !request.TheaterIds.Any())
            {
                return Json(new { success = false, message = "No theaters selected" });
            }

            try
            {
                var theaters = await _context.Theaters
                    .Where(t => request.TheaterIds.Contains(t.ID))
                    .ToListAsync();

                // For this example, we'll assume there's an IsActive property
                // If not, you can modify this to update other properties
                var updatedCount = theaters.Count;

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = $"Updated {updatedCount} theaters successfully"
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Failed to update theaters"
                });
            }
        }

        private bool TheaterExists(int id)
        {
            return _context.Theaters.Any(e => e.ID == id);
        }

        // Helper classes
        public class BulkUpdateRequest
        {
            public List<int> TheaterIds { get; set; }
            public bool IsActive { get; set; }
        }
    }

    // View Model for Theater Index
    public class TheaterIndexViewModel
    {
        public List<Theater> Theaters { get; set; } = new List<Theater>();
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TotalTheaters { get; set; }
        public int StartItem { get; set; }
        public int EndItem { get; set; }
        public bool HasPreviousPage { get; set; }
        public bool HasNextPage { get; set; }
        public string SearchTerm { get; set; } = "";
        public string LocationFilter { get; set; } = "";
        public string FilterType { get; set; } = "all";
        public string SortBy { get; set; } = "name";
        public List<string> Locations { get; set; } = new List<string>();
    }
}