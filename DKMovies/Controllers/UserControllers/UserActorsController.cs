using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

using DKMovies.Models.Data;
using DKMovies.Models.Data.DatabaseModels;

namespace Controllers.UserController
{
    public class UserActorsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public UserActorsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Actors
        public async Task<IActionResult> Index(string searchString, string sortOrder, int page = 1, int pageSize = 12)
        {
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentSort"] = sortOrder;
            ViewData["NameSortParm"] = string.IsNullOrEmpty(sortOrder) ? "name_desc" : "";
            ViewData["DateSortParm"] = sortOrder == "Date" ? "date_desc" : "Date";

            var actors = from a in _context.Actors
                         select a;

            // Search functionality
            if (!string.IsNullOrEmpty(searchString))
            {
                actors = actors.Where(a => a.FullName.Contains(searchString) ||
                                          a.PlaceOfBirth.Contains(searchString));
            }

            // Sorting
            switch (sortOrder)
            {
                case "name_desc":
                    actors = actors.OrderByDescending(a => a.FullName);
                    break;
                case "Date":
                    actors = actors.OrderBy(a => a.DateOfBirth);
                    break;
                case "date_desc":
                    actors = actors.OrderByDescending(a => a.DateOfBirth);
                    break;
                default:
                    actors = actors.OrderBy(a => a.FullName);
                    break;
            }

            // Pagination
            var totalActors = await actors.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalActors / pageSize);

            ViewData["CurrentPage"] = page;
            ViewData["TotalPages"] = totalPages;
            ViewData["PageSize"] = pageSize;

            var pagedActors = await actors
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return View(pagedActors);
        }

        // GET: Actors/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var actor = await _context.Actors
                .Include(a => a.MovieActors)
                    .ThenInclude(ma => ma.Movie)
                        .ThenInclude(m => m.MovieGenres)
                            .ThenInclude(mg => mg.Genre)
                .Include(a => a.MovieActors)
                    .ThenInclude(ma => ma.Movie)
                        .ThenInclude(m => m.Rating)
                .Include(a => a.MovieActors)
                    .ThenInclude(ma => ma.Movie)
                        .ThenInclude(m => m.Reviews.Where(r => r.IsApproved))
                .FirstOrDefaultAsync(a => a.ID == id);


            if (actor == null)
                return NotFound();

            // Calculate age if date of birth is available
            int? age = null;
            if (actor.DateOfBirth.HasValue)
            {
                age = DateTime.Now.Year - actor.DateOfBirth.Value.Year;
                if (DateTime.Now.DayOfYear < actor.DateOfBirth.Value.DayOfYear)
                    age--;
            }
            ViewData["ActorAge"] = age;

            // Get movies with their average ratings
            var moviesWithRatings = actor.MovieActors.Select(ma => new
            {
                ma.Movie,
                ma.Role,
                AverageRating = ma.Movie.Reviews.Any() ? ma.Movie.Reviews.Average(r => r.Rating) : 0
            }).OrderByDescending(m => m.Movie.ReleaseDate).ToList();

            ViewData["MoviesWithRatings"] = moviesWithRatings;

            // Statistics
            ViewData["TotalMovies"] = actor.MovieActors.Count();
            ViewData["GenresWorkedIn"] = actor.MovieActors
                .SelectMany(ma => ma.Movie.MovieGenres.Select(mg => mg.Genre.Name))
                .Distinct()
                .Count();

            return View(actor);
        }
    }
}