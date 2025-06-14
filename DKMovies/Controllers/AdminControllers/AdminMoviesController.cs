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

        // GET: AdminMovies/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var movie = await _context.Movies
                .Include(m => m.Language)
                .Include(m => m.Country)
                .Include(m => m.Director)
                .Include(m => m.MovieGenres)
                    .ThenInclude(mg => mg.Genre)
                .Include(m => m.ShowTimes)
                    .ThenInclude(st => st.Tickets)
                .Include(m => m.Reviews)
                .FirstOrDefaultAsync(m => m.ID == id);

            if (movie == null)
            {
                return NotFound();
            }

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

        // GET: AdminMovies/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var movie = await _context.Movies
                .Include(m => m.Language)
                .Include(m => m.Country)
                .Include(m => m.Director)
                .Include(m => m.MovieGenres)
                    .ThenInclude(mg => mg.Genre)
                .Include(m => m.ShowTimes)
                    .ThenInclude(st => st.Tickets)
                .Include(m => m.Reviews)
                .FirstOrDefaultAsync(m => m.ID == id);

            if (movie == null)
            {
                return NotFound();
            }

            // Populate select lists for dropdowns
            ViewBag.Languages = new SelectList(_context.Languages, "ID", "Name", movie.LanguageID);
            ViewBag.Countries = new SelectList(_context.Countries, "ID", "Name", movie.CountryID);
            ViewBag.Directors = new SelectList(_context.Directors, "ID", "FullName", movie.DirectorID);
            ViewBag.Genres = new MultiSelectList(_context.Genres, "ID", "Name", movie.MovieGenres?.Select(mg => mg.GenreID));

            return View(movie);
        }

        // POST: AdminMovies/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Movie movie, int[] selectedGenres, IFormFile posterImage)
        {
            if (id != movie.ID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Handle poster image upload
                    if (posterImage != null && posterImage.Length > 0)
                    {
                        // Validate file type
                        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                        var fileExtension = Path.GetExtension(posterImage.FileName).ToLowerInvariant();

                        if (allowedExtensions.Contains(fileExtension))
                        {
                            // Create unique filename
                            var fileName = $"{Guid.NewGuid()}{fileExtension}";
                            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "assets", "images", "movie_posters");

                            // Ensure directory exists
                            if (!Directory.Exists(uploadsFolder))
                            {
                                Directory.CreateDirectory(uploadsFolder);
                            }

                            var filePath = Path.Combine(uploadsFolder, fileName);

                            // Save the file
                            using (var fileStream = new FileStream(filePath, FileMode.Create))
                            {
                                await posterImage.CopyToAsync(fileStream);
                            }

                            // Delete old poster if it exists and is not the default
                            if (!string.IsNullOrWhiteSpace(movie.PosterImagePath) &&
                                movie.PosterImagePath != "default-poster.jpg")
                            {
                                var oldFilePath = Path.Combine(uploadsFolder, movie.PosterImagePath.TrimStart('/'));
                                if (System.IO.File.Exists(oldFilePath))
                                {
                                    System.IO.File.Delete(oldFilePath);
                                }
                            }

                            // Update movie poster path
                            movie.PosterImagePath = fileName;
                        }
                        else
                        {
                            ModelState.AddModelError("posterImage", "Please upload a valid image file (jpg, jpeg, png, gif).");
                        }
                    }

                    // Only proceed if no model errors
                    if (ModelState.IsValid)
                    {
                        // Update movie
                        _context.Update(movie);

                        // Update genres - remove existing and add new ones
                        var existingGenres = _context.MovieGenres.Where(mg => mg.MovieID == id);
                        _context.MovieGenres.RemoveRange(existingGenres);

                        if (selectedGenres != null)
                        {
                            foreach (var genreId in selectedGenres)
                            {
                                _context.MovieGenres.Add(new MovieGenre { MovieID = id, GenreID = genreId });
                            }
                        }

                        await _context.SaveChangesAsync();
                        TempData["SuccessMessage"] = "Movie updated successfully.";
                        return RedirectToAction(nameof(Details), new { id = movie.ID });
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!MovieExists(movie.ID))
                        return NotFound();
                    else
                        throw;
                }
                catch (Exception ex)
                {
                    // Log the exception
                    ModelState.AddModelError("", "An error occurred while updating the movie: " + ex.Message);
                }
            }

            // Reload the movie with includes for the view if validation fails
            movie = await _context.Movies
                .Include(m => m.Language)
                .Include(m => m.Country)
                .Include(m => m.Director)
                .Include(m => m.MovieGenres)
                    .ThenInclude(mg => mg.Genre)
                .Include(m => m.ShowTimes)
                    .ThenInclude(st => st.Tickets)
                .Include(m => m.Reviews)
                .FirstOrDefaultAsync(m => m.ID == id);

            // Rebind dropdowns on failure
            ViewBag.Languages = new SelectList(_context.Languages, "ID", "Name", movie.LanguageID);
            ViewBag.Countries = new SelectList(_context.Countries, "ID", "Name", movie.CountryID);
            ViewBag.Directors = new SelectList(_context.Directors, "ID", "FullName", movie.DirectorID);
            ViewBag.Genres = new MultiSelectList(_context.Genres, "ID", "Name", selectedGenres);

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