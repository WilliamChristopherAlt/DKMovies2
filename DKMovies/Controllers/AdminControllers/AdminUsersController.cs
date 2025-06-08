using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DKMovies.Models.ViewModels;

using DKMovies.Models.Data;
using DKMovies.Models.Data.DatabaseModels;

namespace Controllers.Admin
{
    public class AdminUsersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private const int PageSize = 20;

        public AdminUsersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: AdminUser
        public async Task<IActionResult> Index(int page = 1, string search = "", string filter = "all")
        {
            var query = _context.Users
                .Include(u => u.Tickets)
                .Include(u => u.Reviews)
                .AsQueryable();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(u => u.Username.Contains(search) ||
                                       u.Email.Contains(search) ||
                                       u.FullName != null && u.FullName.Contains(search));
            }

            // Apply status filter
            if (filter != "all")
            {
                if (filter == "verified")
                {
                    query = query.Where(u => u.EmailConfirmed);
                }
                else if (filter == "unverified")
                {
                    query = query.Where(u => !u.EmailConfirmed);
                }
                else if (filter == "2fa")
                {
                    query = query.Where(u => u.TwoFactorEnabled);
                }
            }

            // Get total count for pagination
            var totalUsers = await query.CountAsync();

            // Calculate pagination values
            var totalPages = (int)Math.Ceiling((double)totalUsers / PageSize);
            page = Math.Max(1, Math.Min(page, totalPages));

            // Get users for current page
            var users = await query
                .OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            // Create view model with pagination info
            var viewModel = new UserIndexViewModel
            {
                Users = users,
                CurrentPage = page,
                TotalPages = totalPages,
                TotalUsers = totalUsers,
                PageSize = PageSize,
                SearchTerm = search,
                FilterType = filter,
                HasPreviousPage = page > 1,
                HasNextPage = page < totalPages
            };

            return View(viewModel);
        }

        // GET: AdminUser/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var user = await _context.Users
                .Include(u => u.Tickets)
                    .ThenInclude(t => t.ShowTime)
                    .ThenInclude(s => s.Movie)
                .Include(u => u.Reviews)
                    .ThenInclude(r => r.Movie)
                .Include(u => u.LoginAttempts)
                .FirstOrDefaultAsync(u => u.ID == id);

            if (user == null) return NotFound();

            return View(user);
        }

        // GET: AdminUser/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            return View(user);
        }

        // POST: AdminUser/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ID,Username,Email,FullName,Phone,BirthDate,Gender,EmailConfirmed,TwoFactorEnabled")] User user)
        {
            if (id != user.ID) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var existingUser = await _context.Users.FindAsync(id);
                    if (existingUser == null) return NotFound();

                    // Update only allowed fields
                    existingUser.Username = user.Username;
                    existingUser.Email = user.Email;
                    existingUser.FullName = user.FullName;
                    existingUser.Phone = user.Phone;
                    existingUser.BirthDate = user.BirthDate;
                    existingUser.Gender = user.Gender;
                    existingUser.EmailConfirmed = user.EmailConfirmed;
                    existingUser.TwoFactorEnabled = user.TwoFactorEnabled;

                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "User updated successfully.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!UserExists(user.ID)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(user);
        }

        // GET: AdminUser/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var user = await _context.Users
                .Include(u => u.Tickets)
                .Include(u => u.Reviews)
                .FirstOrDefaultAsync(u => u.ID == id);

            if (user == null) return NotFound();

            return View(user);
        }

        // POST: AdminUser/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                // Note: Consider soft delete or checking for related data first
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "User deleted successfully.";
            }
            return RedirectToAction(nameof(Index));
        }

        private bool UserExists(int id)
        {
            return _context.Users.Any(e => e.ID == id);
        }
    }
}