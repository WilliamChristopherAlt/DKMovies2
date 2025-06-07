using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DKMovies.Models.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;

using DKMovies.Models.Data;
using DKMovies.Models.Data.DatabaseModels;

namespace Controllers.Admin
{
    public class AdminEmployeeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private const int PageSize = 20;

        public AdminEmployeeController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: AdminEmployee
        public async Task<IActionResult> Index(int page = 1, string search = "", string filter = "all")
        {
            var query = _context.Employees
                .Include(e => e.Theater)
                .Include(e => e.Role)
                .Include(e => e.Admins)
                .AsQueryable();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(e => e.FullName.Contains(search) ||
                                       e.Email.Contains(search) ||
                                       e.Phone.Contains(search) ||
                                       e.CitizenID.Contains(search) ||
                                       e.Theater.Name.Contains(search) ||
                                       e.Role.Name.Contains(search));
            }

            // Apply status filter
            if (filter != "all")
            {
                var currentDate = DateTime.Now;
                if (filter == "recent")
                {
                    // Employees hired in the last 30 days
                    var thirtyDaysAgo = currentDate.AddDays(-30);
                    query = query.Where(e => e.HireDate >= thirtyDaysAgo);
                }
                else if (filter == "senior")
                {
                    // Employees hired more than 2 years ago
                    var twoYearsAgo = currentDate.AddYears(-2);
                    query = query.Where(e => e.HireDate <= twoYearsAgo);
                }
                else if (filter == "admin")
                {
                    // Employees who are also admins
                    query = query.Where(e => e.Admins.Any());
                }
            }

            // Get total count for pagination
            var totalEmployees = await query.CountAsync();

            // Calculate pagination values
            var totalPages = (int)Math.Ceiling((double)totalEmployees / PageSize);
            page = Math.Max(1, Math.Min(page, totalPages));

            // Get employees for current page
            var employees = await query
                .OrderByDescending(e => e.HireDate)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            // Create view model with pagination info
            var viewModel = new EmployeeIndexViewModel
            {
                Employees = employees,
                CurrentPage = page,
                TotalPages = totalPages,
                TotalEmployees = totalEmployees,
                PageSize = PageSize,
                SearchTerm = search,
                FilterType = filter,
                HasPreviousPage = page > 1,
                HasNextPage = page < totalPages
            };

            return View(viewModel);
        }

        // GET: AdminEmployee/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var employee = await _context.Employees
                .Include(e => e.Theater)
                .Include(e => e.Role)
                .Include(e => e.Admins)
                .FirstOrDefaultAsync(e => e.ID == id);

            if (employee == null) return NotFound();

            return View(employee);
        }

        // GET: AdminEmployee/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var employee = await _context.Employees
                .Include(e => e.Theater)
                .Include(e => e.Role)
                .FirstOrDefaultAsync(e => e.ID == id);

            if (employee == null) return NotFound();

            // Load dropdown data
            ViewBag.TheaterID = new SelectList(_context.Theaters, "ID", "Name", employee.TheaterID);
            ViewBag.RoleID = new SelectList(_context.EmployeeRoles, "ID", "Name", employee.RoleID);

            return View(employee);
        }

        // POST: AdminEmployee/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ID,TheaterID,RoleID,FullName,Email,Phone,Gender,DateOfBirth,CitizenID,Address,HireDate,Salary")] Employee employee)
        {
            if (id != employee.ID) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var existingEmployee = await _context.Employees.FindAsync(id);
                    if (existingEmployee == null) return NotFound();

                    // Update only allowed fields
                    existingEmployee.TheaterID = employee.TheaterID;
                    existingEmployee.RoleID = employee.RoleID;
                    existingEmployee.FullName = employee.FullName;
                    existingEmployee.Email = employee.Email;
                    existingEmployee.Phone = employee.Phone;
                    existingEmployee.Gender = employee.Gender;
                    existingEmployee.DateOfBirth = employee.DateOfBirth;
                    existingEmployee.CitizenID = employee.CitizenID;
                    existingEmployee.Address = employee.Address;
                    existingEmployee.HireDate = employee.HireDate;
                    existingEmployee.Salary = employee.Salary;

                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Employee updated successfully.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!EmployeeExists(employee.ID)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }

            // Reload dropdown data if validation fails
            ViewBag.TheaterID = new SelectList(_context.Theaters, "ID", "Name", employee.TheaterID);
            ViewBag.RoleID = new SelectList(_context.EmployeeRoles, "ID", "Name", employee.RoleID);
            return View(employee);
        }

        // GET: AdminEmployee/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var employee = await _context.Employees
                .Include(e => e.Theater)
                .Include(e => e.Role)
                .Include(e => e.Admins)
                .FirstOrDefaultAsync(e => e.ID == id);

            if (employee == null) return NotFound();

            return View(employee);
        }

        // POST: AdminEmployee/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var employee = await _context.Employees.FindAsync(id);
            if (employee != null)
            {
                // Note: Consider soft delete or checking for related data first
                _context.Employees.Remove(employee);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Employee deleted successfully.";
            }
            return RedirectToAction(nameof(Index));
        }

        private bool EmployeeExists(int id)
        {
            return _context.Employees.Any(e => e.ID == id);
        }
    }
}