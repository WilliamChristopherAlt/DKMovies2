using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using DKMovies.Models.ViewModels;

using DKMovies.Models.Data;
using DKMovies.Models.Data.DatabaseModels;

namespace Controllers.Admin
{
    public class AdminShowTimesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private const int PageSize = 12; // Show 12 showtimes per page (good for grid layout)

        public AdminShowTimesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: AdminShowTime
        public async Task<IActionResult> Index(int page = 1, string search = "", string filter = "all")
        {
            var query = _context.ShowTimes
                .Include(s => s.Movie)
                .Include(s => s.Auditorium)
                    .ThenInclude(a => a.Theater)
                .AsQueryable();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(s => s.Movie.Title.Contains(search) ||
                                       s.Auditorium.Theater.Name.Contains(search) ||
                                       s.Auditorium.Name.Contains(search));
            }

            // Apply status filter
            var now = DateTime.Now;
            if (filter != "all")
            {
                if (filter == "showing")
                {
                    // Currently showing (started but not finished)
                    query = query.Where(s => s.StartTime <= now &&
                                           s.StartTime.AddMinutes(s.DurationMinutes) > now);
                }
                else if (filter == "upcoming")
                {
                    // Not started yet
                    query = query.Where(s => s.StartTime > now);
                }
                else if (filter == "past")
                {
                    // Already finished
                    query = query.Where(s => s.StartTime.AddMinutes(s.DurationMinutes) < now);
                }
            }

            // Get total count for pagination
            var totalShowtimes = await query.CountAsync();

            // Calculate pagination values
            var totalPages = (int)Math.Ceiling((double)totalShowtimes / PageSize);
            page = Math.Max(1, Math.Min(page, totalPages)); // Ensure page is within valid range

            // Get showtimes for current page, ordered by start time (newest first)
            var showtimes = await query
                .OrderByDescending(s => s.StartTime)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            // Get statistics for all showtimes (not just current page)
            var allStats = await _context.ShowTimes.Select(s => new
            {
                s.StartTime,
                s.DurationMinutes
            }).ToListAsync();

            var stats = new
            {
                Total = allStats.Count,
                Upcoming = allStats.Count(s => s.StartTime > now),
                Showing = allStats.Count(s => s.StartTime <= now &&
                                              s.StartTime.AddMinutes(s.DurationMinutes) > now),
                Past = allStats.Count(s => s.StartTime.AddMinutes(s.DurationMinutes) < now)
            };

            // Create view model with pagination info
            var viewModel = new ShowTimeIndexViewModel
            {
                ShowTimes = showtimes,
                CurrentPage = page,
                TotalPages = totalPages,
                TotalShowtimes = totalShowtimes,
                PageSize = PageSize,
                SearchTerm = search,
                FilterType = filter,
                HasPreviousPage = page > 1,
                HasNextPage = page < totalPages,
                Statistics = stats
            };

            return View(viewModel);
        }

        // GET: AdminShowTime/Create
        public IActionResult Create()
        {
            ViewData["MovieID"] = new SelectList(_context.Movies, "ID", "Title");
            ViewData["AuditoriumID"] = new SelectList(
                _context.Auditoriums.Include(a => a.Theater)
                .Select(a => new
                {
                    a.ID,
                    Name = a.Theater.Name + " - Phòng " + a.Name
                }),
                "ID", "Name"
            );
            return View();
        }

        // POST: AdminShowTime/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ID,MovieID,AuditoriumID,StartTime,Price")] ShowTime showTime)
        {
            if (ModelState.IsValid)
            {
                _context.Add(showTime);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Thêm suất chiếu thành công!";
                return RedirectToAction(nameof(Index));
            }

            ViewData["MovieID"] = new SelectList(_context.Movies, "ID", "Title", showTime.MovieID);
            ViewData["AuditoriumID"] = new SelectList(_context.Auditoriums, "ID", "Name", showTime.AuditoriumID);
            TempData["ErrorMessage"] = "Lỗi khi thêm suất chiếu.";
            return View(showTime);
        }

        // GET: AdminShowTime/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var showTime = await _context.ShowTimes.FindAsync(id);
            if (showTime == null) return NotFound();

            ViewData["MovieID"] = new SelectList(_context.Movies, "ID", "Title", showTime.MovieID);
            ViewData["AuditoriumID"] = new SelectList(_context.Auditoriums.Include(a => a.Theater)
                .Select(a => new
                {
                    a.ID,
                    Name = a.Theater.Name + " - Phòng " + a.Name
                }), "ID", "Name", showTime.AuditoriumID);
            return View(showTime);
        }

        // POST: AdminShowTime/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ID,MovieID,AuditoriumID,StartTime,Price")] ShowTime showTime)
        {
            if (id != showTime.ID) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(showTime);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Cập nhật thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.ShowTimes.Any(e => e.ID == showTime.ID))
                        return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }

            TempData["ErrorMessage"] = "Lỗi khi cập nhật.";
            return View(showTime);
        }

        // GET: AdminShowTime/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var showTime = await _context.ShowTimes
                .Include(s => s.Movie)
                .Include(s => s.Auditorium)
                    .ThenInclude(a => a.Theater)
                .FirstOrDefaultAsync(m => m.ID == id);

            if (showTime == null) return NotFound();

            return View(showTime);
        }

        // POST: AdminShowTime/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var showTime = await _context.ShowTimes.FindAsync(id);
            if (showTime != null)
            {
                _context.ShowTimes.Remove(showTime);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Xóa thành công!";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
