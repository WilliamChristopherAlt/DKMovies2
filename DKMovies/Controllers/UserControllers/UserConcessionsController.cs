using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DKMovies.Models.Data.DatabaseModels;
using DKMovies.Models.Data;

namespace DKMovies.Controllers
{
    public class UserConcessionsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public UserConcessionsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: UserConcessions
        public async Task<IActionResult> Index(int page = 1, int pageSize = 20)
        {
            var concessions = _context.Concessions.AsQueryable();

            // Get total count for pagination
            var totalConcessions = await concessions.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalConcessions / pageSize);

            // Apply pagination
            var paginatedConcessions = await concessions
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Get popular concessions based on order items sold
            var popularConcessions = await _context.Concessions
                .Select(c => new {
                    Concession = c,
                    TotalSold = _context.OrderItems
                        .Where(oi => oi.TheaterConcession.ConcessionID == c.ID)
                        .Sum(oi => oi.Quantity)
                })
                .OrderByDescending(x => x.TotalSold)
                .Take(8)
                .Select(x => x.Concession)
                .ToListAsync();

            // Pass data to view
            ViewData["CurrentPage"] = page;
            ViewData["TotalPages"] = totalPages;
            ViewBag.PopularConcessions = popularConcessions;

            return View(paginatedConcessions);
        }

        // GET: UserConcessions/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var concession = await _context.Concessions
                .FirstOrDefaultAsync(m => m.ID == id);

            if (concession == null)
            {
                return NotFound();
            }

            return View(concession);
        }
    }
}