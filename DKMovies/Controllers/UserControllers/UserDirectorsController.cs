using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;
using DKMovies.Models.Data;
using DKMovies.Models.Data.DatabaseModels;

namespace Controllers.UserController
{
    public class UserDirectorsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public UserDirectorsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: DirectorsList
        public async Task<IActionResult> Index(string searchString, string sortOrder, int page = 1, int pageSize = 12)
        {
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentSort"] = sortOrder;
            ViewData["NameSortParm"] = string.IsNullOrEmpty(sortOrder) ? "name_desc" : "";
            ViewData["DateSortParm"] = sortOrder == "Date" ? "date_desc" : "Date";

            var directors = from d in _context.Directors
                            select d;

            // Search functionality
            if (!string.IsNullOrEmpty(searchString))
            {
                directors = directors.Where(d => d.FullName.Contains(searchString) ||
                                              d.PlaceOfBirth.Contains(searchString));
            }

            // Sorting
            switch (sortOrder)
            {
                case "name_desc":
                    directors = directors.OrderByDescending(d => d.FullName);
                    break;
                case "Date":
                    directors = directors.OrderBy(d => d.DateOfBirth);
                    break;
                case "date_desc":
                    directors = directors.OrderByDescending(d => d.DateOfBirth);
                    break;
                default:
                    directors = directors.OrderBy(d => d.FullName);
                    break;
            }

            // Pagination
            var totalDirectors = await directors.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalDirectors / pageSize);

            ViewData["CurrentPage"] = page;
            ViewData["TotalPages"] = totalPages;
            ViewData["PageSize"] = pageSize;

            var pagedDirectors = await directors
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return View(pagedDirectors);
        }

        // GET: DirectorsList/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var director = await _context.Directors
                .Include(d => d.Movies)
                    .ThenInclude(m => m.Reviews) // Include reviews to calculate ratings
                .Include(d => d.Movies)
                    .ThenInclude(m => m.MovieGenres) // Include movie genres
                        .ThenInclude(mg => mg.Genre) // Include the actual genre
                .FirstOrDefaultAsync(d => d.ID == id);

            if (director == null)
            {
                TempData["Error"] = "Director not found.";
                return RedirectToAction("Index", "MoviesList");
            }

            // Calculate director's age
            int? directorAge = null;
            if (director.DateOfBirth.HasValue)
            {
                var today = DateTime.Today;
                directorAge = today.Year - director.DateOfBirth.Value.Year;
                if (director.DateOfBirth.Value.Date > today.AddYears(-directorAge.Value))
                {
                    directorAge--;
                }
            }

            // Get total movies count
            var totalMovies = director.Movies?.Count ?? 0;

            // Calculate unique genres worked in
            var genresWorkedIn = director.Movies?
                .SelectMany(m => m.MovieGenres ?? new List<MovieGenre>())
                .Select(mg => mg.GenreID)
                .Distinct()
                .Count() ?? 0;

            // Prepare movies with their average ratings
            var moviesWithRatings = director.Movies?
                .Select(movie => new
                {
                    Movie = movie,
                    AverageRating = movie.Reviews != null && movie.Reviews.Any()
                        ? movie.Reviews.Average(r => r.Rating)
                        : 0.0
                })
                .OrderByDescending(m => m.Movie.ReleaseDate)
                .ToList();

            // Set ViewData for the view
            ViewData["DirectorAge"] = directorAge;
            ViewData["TotalMovies"] = totalMovies;
            ViewData["GenresWorkedIn"] = genresWorkedIn;
            ViewData["MoviesWithRatings"] = moviesWithRatings;

            return View(director);
        }
    }
}