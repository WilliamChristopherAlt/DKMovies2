using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using DKMovies.Models.ViewModels;
using DKMovies.Models.Data;
using DKMovies.Models.Data.DatabaseModels;

namespace Controllers.Admin
{
    public class AdminMoviesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private const int PageSize = 20;

        public AdminMoviesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: AdminMovie
        public async Task<IActionResult> Index(int page = 1, string search = "", string filter = "all")
        {
            var query = _context.Movies
                .Include(m => m.MovieGenres)
                    .ThenInclude(mg => mg.Genre)
                .Include(m => m.Director)
                .Include(m => m.Language)
                .Include(m => m.Rating)
                .AsQueryable();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(m => m.Title.Contains(search) ||
                                       m.Director != null && m.Director.FullName.Contains(search));
            }

            // Apply status filter
            if (filter != "all")
            {
                var now = DateTime.Now;
                if (filter == "showing")
                {
                    query = query.Where(m => m.ReleaseDate.HasValue && m.ReleaseDate.Value <= now);
                }
                else if (filter == "upcoming")
                {
                    query = query.Where(m => !m.ReleaseDate.HasValue || m.ReleaseDate.Value > now);
                }
            }

            // Get total count for pagination
            var totalMovies = await query.CountAsync();

            // Calculate pagination values
            var totalPages = (int)Math.Ceiling((double)totalMovies / PageSize);
            page = Math.Max(1, Math.Min(page, totalPages)); // Ensure page is within valid range

            // Get movies for current page
            var movies = await query
                .OrderBy(m => m.Title)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            // Create view model with pagination info
            var viewModel = new MovieIndexViewModel
            {
                Movies = movies,
                CurrentPage = page,
                TotalPages = totalPages,
                TotalMovies = totalMovies,
                PageSize = PageSize,
                SearchTerm = search,
                FilterType = filter,
                HasPreviousPage = page > 1,
                HasNextPage = page < totalPages
            };

            return View(viewModel);
        }

        // GET: AdminMovie/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var movie = await _context.Movies
                .Include(m => m.MovieGenres)
                .FirstOrDefaultAsync(m => m.ID == id);
            if (movie == null) return NotFound();

            return View(movie);
        }

        // GET: AdminMovie/Create
        public IActionResult Create()
        {
            ViewData["GenreID"] = new SelectList(_context.Genres, "ID", "Name");
            return View();
        }

        // POST: AdminMovie/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ID,Title,Description,Duration,ReleaseDate,GenreID,PosterUrl,TrailerUrl")] Movie movie)
        {
            if (ModelState.IsValid)
            {
                _context.Add(movie);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đã thêm phim thành công.";
                return RedirectToAction(nameof(Index));
            }
            ViewData["GenreID"] = new SelectList(_context.Genres, "ID", "Name", movie.MovieGenres);
            return View(movie);
        }

        // GET: AdminMovie/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var movie = await _context.Movies.FindAsync(id);
            if (movie == null) return NotFound();

            ViewData["GenreID"] = new SelectList(_context.Genres, "ID", "Name", movie.MovieGenres);
            return View(movie);
        }

        // POST: AdminMovie/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ID,Title,Description,Duration,ReleaseDate,GenreID,PosterUrl,TrailerUrl")] Movie movie)
        {
            if (id != movie.ID) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(movie);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Đã cập nhật phim.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!MovieExists(movie.ID)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }

            ViewData["GenreID"] = new SelectList(_context.Genres, "ID", "Name", movie.MovieGenres);
            return View(movie);
        }

        // GET: AdminMovie/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var movie = await _context.Movies
                .Include(m => m.MovieGenres)
                .FirstOrDefaultAsync(m => m.ID == id);
            if (movie == null) return NotFound();

            return View(movie);
        }

        // POST: AdminMovie/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var movie = await _context.Movies.FindAsync(id);
            if (movie != null)
            {
                _context.Movies.Remove(movie);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đã xóa phim.";
            }
            return RedirectToAction(nameof(Index));
        }

        private bool MovieExists(int id)
        {
            return _context.Movies.Any(e => e.ID == id);
        }
    }
}
