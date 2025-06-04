using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DKMovies.Models;
using System.Security.Claims;

namespace DKMovies.Controllers
{
    public class ActorsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ActorsController(ApplicationDbContext context)
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
                Movie = ma.Movie,
                Role = ma.Role,
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

        // GET: Actors/Create
        public IActionResult Create()
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            if (role != "Admin")
            {
                return Forbid();
            }
            return View();
        }

        // POST: Actors/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ID,FullName,Biography,DateOfBirth,PlaceOfBirth,ProfileImagePath")] Actor actor)
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            if (role != "Admin")
            {
                return Forbid();
            }

            if (ModelState.IsValid)
            {
                _context.Add(actor);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(actor);
        }

        // GET: Actors/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            if (role != "Admin")
            {
                return Forbid();
            }

            if (id == null)
                return NotFound();

            var actor = await _context.Actors.FindAsync(id);
            if (actor == null)
                return NotFound();

            return View(actor);
        }

        // POST: Actors/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ID,FullName,Biography,DateOfBirth,PlaceOfBirth,ProfileImagePath")] Actor actor)
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            if (role != "Admin")
            {
                return Forbid();
            }

            if (id != actor.ID)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(actor);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ActorExists(actor.ID))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(actor);
        }

        // GET: Actors/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            if (role != "Admin")
            {
                return Forbid();
            }

            if (id == null)
                return NotFound();

            var actor = await _context.Actors
                .FirstOrDefaultAsync(a => a.ID == id);
            if (actor == null)
                return NotFound();

            return View(actor);
        }

        // POST: Actors/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            if (role != "Admin")
            {
                return Forbid();
            }

            var actor = await _context.Actors.FindAsync(id);
            if (actor != null)
            {
                _context.Actors.Remove(actor);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        private bool ActorExists(int id)
        {
            return _context.Actors.Any(e => e.ID == id);
        }
    }
}