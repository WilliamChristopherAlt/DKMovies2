using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DKMovies.Models.ViewModels;

using DKMovies.Models.Data;
using DKMovies.Models.Data.DatabaseModels;

namespace Controllers.Admin
{
    public class AdminConcessionsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private const int PageSize = 20;

        public AdminConcessionsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: AdminConcession
        public async Task<IActionResult> Index(int page = 1, string search = "")
        {
            var query = _context.Concessions.AsQueryable();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(c => c.Name.Contains(search) ||
                                       c.Description != null && c.Description.Contains(search));
            }

            // Get total count for pagination
            var totalConcessions = await query.CountAsync();

            // Calculate pagination values
            var totalPages = (int)Math.Ceiling((double)totalConcessions / PageSize);
            page = Math.Max(1, Math.Min(page, totalPages));

            // Get concessions for current page
            var concessions = await query
                .OrderBy(c => c.Name)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            // Create view model with pagination info
            var viewModel = new ConcessionIndexViewModel
            {
                Concessions = concessions,
                CurrentPage = page,
                TotalPages = totalPages,
                TotalConcessions = totalConcessions,
                PageSize = PageSize,
                SearchTerm = search,
                HasPreviousPage = page > 1,
                HasNextPage = page < totalPages
            };

            return View(viewModel);
        }

        // GET: AdminConcession/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var concession = await _context.Concessions
                .Include(c => c.TheaterConcessions)
                .FirstOrDefaultAsync(c => c.ID == id);

            if (concession == null) return NotFound();

            return View(concession);
        }

        // GET: AdminConcession/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: AdminConcession/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ID,Name,Description,ImagePath")] Concession concession)
        {
            if (ModelState.IsValid)
            {
                _context.Add(concession);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Concession added successfully.";
                return RedirectToAction(nameof(Index));
            }
            return View(concession);
        }

        // GET: AdminConcession/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var concession = await _context.Concessions.FindAsync(id);
            if (concession == null) return NotFound();

            return View(concession);
        }

        // POST: AdminConcession/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ID,Name,Description,ImagePath")] Concession concession)
        {
            if (id != concession.ID) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(concession);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Concession updated successfully.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ConcessionExists(concession.ID)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(concession);
        }

        // GET: AdminConcession/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var concession = await _context.Concessions
                .FirstOrDefaultAsync(c => c.ID == id);

            if (concession == null) return NotFound();

            return View(concession);
        }

        // POST: AdminConcession/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var concession = await _context.Concessions.FindAsync(id);
            if (concession != null)
            {
                _context.Concessions.Remove(concession);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Concession deleted successfully.";
            }
            return RedirectToAction(nameof(Index));
        }

        private bool ConcessionExists(int id)
        {
            return _context.Concessions.Any(e => e.ID == id);
        }
    }
}