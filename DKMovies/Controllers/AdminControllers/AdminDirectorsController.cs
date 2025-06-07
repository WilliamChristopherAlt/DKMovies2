using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DKMovies.Models.ViewModels;
using DKMovies.Models.Data;
using DKMovies.Models.Data.DatabaseModels;

namespace Controllers.Admin
{
    public class AdminDirectorsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private const int PageSize = 20;

        public AdminDirectorsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: AdminDirector
        public async Task<IActionResult> Index(int page = 1, string search = "", string filter = "all")
        {
            var query = _context.Directors
                .Include(d => d.Movies)
                .AsQueryable();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(d => d.FullName.Contains(search) ||
                                       d.Biography.Contains(search) ||
                                       d.PlaceOfBirth.Contains(search));
            }

            // Apply status filter
            if (filter != "all")
            {
                var currentDate = DateTime.Now;
                if (filter == "recent")
                {
                    // Directors born in the last 30 years (under 30)
                    var thirtyYearsAgo = currentDate.AddYears(-30);
                    query = query.Where(d => d.DateOfBirth >= thirtyYearsAgo);
                }
                else if (filter == "veteran")
                {
                    // Directors born more than 50 years ago (over 50)
                    var fiftyYearsAgo = currentDate.AddYears(-50);
                    query = query.Where(d => d.DateOfBirth <= fiftyYearsAgo);
                }
                else if (filter == "active")
                {
                    // Directors who have movies
                    query = query.Where(d => d.Movies.Any());
                }
            }

            // Get total count for pagination
            var totalDirectors = await query.CountAsync();

            // Calculate pagination values
            var totalPages = (int)Math.Ceiling((double)totalDirectors / PageSize);
            page = Math.Max(1, Math.Min(page, totalPages));

            // Get directors for current page
            var directors = await query
                .OrderBy(d => d.FullName)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            // Create view model with pagination info
            var viewModel = new DirectorIndexViewModel
            {
                Directors = directors,
                CurrentPage = page,
                TotalPages = totalPages,
                TotalDirectors = totalDirectors,
                PageSize = PageSize,
                SearchTerm = search,
                FilterType = filter,
                HasPreviousPage = page > 1,
                HasNextPage = page < totalPages
            };

            return View(viewModel);
        }

        // GET: AdminDirector/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var director = await _context.Directors
                .Include(d => d.Movies)
                .FirstOrDefaultAsync(d => d.ID == id);

            if (director == null) return NotFound();

            return View(director);
        }

        // GET: AdminDirector/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: AdminDirector/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("FullName,Biography,DateOfBirth,PlaceOfBirth,ProfileImagePath")] Director director)
        {
            if (ModelState.IsValid)
            {
                _context.Add(director);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Director created successfully.";
                return RedirectToAction(nameof(Index));
            }
            return View(director);
        }

        // GET: AdminDirector/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var director = await _context.Directors.FindAsync(id);
            if (director == null) return NotFound();

            return View(director);
        }

        // POST: AdminDirector/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ID,FullName,Biography,DateOfBirth,PlaceOfBirth,ProfileImagePath")] Director director)
        {
            if (id != director.ID) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var existingDirector = await _context.Directors.FindAsync(id);
                    if (existingDirector == null) return NotFound();

                    // Update only allowed fields
                    existingDirector.FullName = director.FullName;
                    existingDirector.Biography = director.Biography;
                    existingDirector.DateOfBirth = director.DateOfBirth;
                    existingDirector.PlaceOfBirth = director.PlaceOfBirth;
                    existingDirector.ProfileImagePath = director.ProfileImagePath;

                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Director updated successfully.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!DirectorExists(director.ID)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(director);
        }

        // GET: AdminDirector/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var director = await _context.Directors
                .Include(d => d.Movies)
                .FirstOrDefaultAsync(d => d.ID == id);

            if (director == null) return NotFound();

            return View(director);
        }

        // POST: AdminDirector/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var director = await _context.Directors.FindAsync(id);
            if (director != null)
            {
                _context.Directors.Remove(director);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Director deleted successfully.";
            }
            return RedirectToAction(nameof(Index));
        }

        private bool DirectorExists(int id)
        {
            return _context.Directors.Any(d => d.ID == id);
        }
    }
}