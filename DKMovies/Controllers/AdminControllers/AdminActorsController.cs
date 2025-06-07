using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DKMovies.Models.ViewModels;

using DKMovies.Models.Data;
using DKMovies.Models.Data.DatabaseModels;

namespace Controllers.Admin
{
    public class AdminActorsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private const int PageSize = 20;

        public AdminActorsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: AdminActor
        public async Task<IActionResult> Index(int page = 1, string search = "", string filter = "all")
        {
            var query = _context.Actors
                .Include(a => a.MovieActors)
                .ThenInclude(ma => ma.Movie)
                .AsQueryable();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(a => a.FullName.Contains(search) ||
                                       a.Biography.Contains(search) ||
                                       a.PlaceOfBirth.Contains(search));
            }

            // Apply status filter
            if (filter != "all")
            {
                var currentDate = DateTime.Now;
                if (filter == "recent")
                {
                    // Actors born in the last 30 years (under 30)
                    var thirtyYearsAgo = currentDate.AddYears(-30);
                    query = query.Where(a => a.DateOfBirth >= thirtyYearsAgo);
                }
                else if (filter == "veteran")
                {
                    // Actors born more than 50 years ago (over 50)
                    var fiftyYearsAgo = currentDate.AddYears(-50);
                    query = query.Where(a => a.DateOfBirth <= fiftyYearsAgo);
                }
                else if (filter == "active")
                {
                    // Actors who have movies
                    query = query.Where(a => a.MovieActors.Any());
                }
            }

            // Get total count for pagination
            var totalActors = await query.CountAsync();

            // Calculate pagination values
            var totalPages = (int)Math.Ceiling((double)totalActors / PageSize);
            page = Math.Max(1, Math.Min(page, totalPages));

            // Get actors for current page
            var actors = await query
                .OrderBy(a => a.FullName)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            // Create view model with pagination info
            var viewModel = new ActorIndexViewModel
            {
                Actors = actors,
                CurrentPage = page,
                TotalPages = totalPages,
                TotalActors = totalActors,
                PageSize = PageSize,
                SearchTerm = search,
                FilterType = filter,
                HasPreviousPage = page > 1,
                HasNextPage = page < totalPages
            };

            return View(viewModel);
        }

        // GET: AdminActor/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var actor = await _context.Actors
                .Include(a => a.MovieActors)
                .ThenInclude(ma => ma.Movie)
                .FirstOrDefaultAsync(a => a.ID == id);

            if (actor == null) return NotFound();

            return View(actor);
        }

        // GET: AdminActor/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: AdminActor/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("FullName,Biography,DateOfBirth,PlaceOfBirth,ProfileImagePath")] Actor actor)
        {
            if (ModelState.IsValid)
            {
                _context.Add(actor);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Actor created successfully.";
                return RedirectToAction(nameof(Index));
            }
            return View(actor);
        }

        // GET: AdminActor/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var actor = await _context.Actors.FindAsync(id);
            if (actor == null) return NotFound();

            return View(actor);
        }

        // POST: AdminActor/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ID,FullName,Biography,DateOfBirth,PlaceOfBirth,ProfileImagePath")] Actor actor)
        {
            if (id != actor.ID) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var existingActor = await _context.Actors.FindAsync(id);
                    if (existingActor == null) return NotFound();

                    // Update only allowed fields
                    existingActor.FullName = actor.FullName;
                    existingActor.Biography = actor.Biography;
                    existingActor.DateOfBirth = actor.DateOfBirth;
                    existingActor.PlaceOfBirth = actor.PlaceOfBirth;
                    existingActor.ProfileImagePath = actor.ProfileImagePath;

                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Actor updated successfully.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ActorExists(actor.ID)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(actor);
        }

        // GET: AdminActor/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var actor = await _context.Actors
                .Include(a => a.MovieActors)
                .ThenInclude(ma => ma.Movie)
                .FirstOrDefaultAsync(a => a.ID == id);

            if (actor == null) return NotFound();

            return View(actor);
        }

        // POST: AdminActor/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var actor = await _context.Actors.FindAsync(id);
            if (actor != null)
            {
                // Note: Consider soft delete or checking for related data first
                _context.Actors.Remove(actor);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Actor deleted successfully.";
            }
            return RedirectToAction(nameof(Index));
        }

        private bool ActorExists(int id)
        {
            return _context.Actors.Any(a => a.ID == id);
        }
    }
}