using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using DKMovies.Models.Data;
using DKMovies.Models.Data.DatabaseModels;

namespace Controllers.AdminControllers
{
    [Authorize(Roles = "Admin")]
    public class AdminProfileController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminProfileController(ApplicationDbContext context)
        {
            _context = context;
        }

        private string HashPassword(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(bytes);
            }
        }

        private bool VerifyPassword(string password, string hash)
        {
            return HashPassword(password) == hash;
        }

        private void RemoveNavigationModelState()
        {
            ModelState.Remove(nameof(DKMovies.Models.Data.DatabaseModels.Admin.Employee));
            ModelState.Remove(nameof(DKMovies.Models.Data.DatabaseModels.Admin.Notifications));
            ModelState.Remove(nameof(DKMovies.Models.Data.DatabaseModels.Admin.LoginAttempts));
            ModelState.Remove(nameof(DKMovies.Models.Data.DatabaseModels.Admin.PasswordHash));
        }

        // GET: /AdminProfile/Index
        public async Task<IActionResult> Index()
        {
            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int adminId))
                return Unauthorized();

            var admin = await _context.Admins
                .Include(a => a.Employee)
                .ThenInclude(e => e.Role)
                .Include(a => a.Employee.Theater)
                .FirstOrDefaultAsync(a => a.ID == adminId);

            if (admin == null)
                return NotFound();

            ViewBag.ToastMessage = TempData["ToastSuccess"];
            ViewBag.ToastError = TempData["ToastError"];

            return View(admin);
        }

        // POST: /AdminProfile/Index (Update Profile)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(string Username, string CurrentPassword, string NewPassword)
        {
            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int adminId))
                return Unauthorized();

            // Remove navigation properties from ModelState validation
            RemoveNavigationModelState();

            // Clear password-related ModelState entries to handle them manually
            ModelState.Remove("CurrentPassword");
            ModelState.Remove("NewPassword");

            // Check for duplicate username
            if (await _context.Admins.AnyAsync(a => a.Username == Username && a.ID != adminId))
            {
                ModelState.AddModelError("Username", "Username already exists.");
            }

            // Determine if user wants to change password
            bool wantsToChangePassword = !string.IsNullOrWhiteSpace(NewPassword);

            // Validate password change only if new password is provided
            if (wantsToChangePassword)
            {
                if (string.IsNullOrWhiteSpace(CurrentPassword))
                {
                    ModelState.AddModelError("CurrentPassword", "Current password is required to change password.");
                }
                else
                {
                    // Get current admin to verify current password
                    var adminForPasswordCheck = await _context.Admins.FirstOrDefaultAsync(a => a.ID == adminId);
                    if (adminForPasswordCheck == null)
                        return NotFound();

                    if (!VerifyPassword(CurrentPassword.Trim(), adminForPasswordCheck.PasswordHash))
                    {
                        ModelState.AddModelError("CurrentPassword", "Current password is incorrect.");
                    }
                    else
                    {
                        // Validate password strength
                        var passwordErrors = ValidatePasswordStrength(NewPassword.Trim());
                        if (passwordErrors.Any())
                        {
                            foreach (var error in passwordErrors)
                            {
                                ModelState.AddModelError("NewPassword", error);
                            }
                        }
                    }
                }
            }
            // If current password is provided but new password is not, show error
            else if (!string.IsNullOrWhiteSpace(CurrentPassword))
            {
                ModelState.AddModelError("NewPassword", "New password is required when current password is provided.");
            }

            if (!ModelState.IsValid)
            {
                // Reload admin data for view
                var admin = await _context.Admins
                    .Include(a => a.Employee)
                    .ThenInclude(e => e.Role)
                    .Include(a => a.Employee.Theater)
                    .FirstOrDefaultAsync(a => a.ID == adminId);

                if (admin == null)
                    return NotFound();

                // Update the username in the model for display
                admin.Username = Username;

                return View(admin);
            }

            try
            {
                // Get the existing admin from database
                var existingAdmin = await _context.Admins.FirstOrDefaultAsync(a => a.ID == adminId);
                if (existingAdmin == null)
                    return NotFound();

                bool hasChanges = false;

                // Update username if changed
                if (!string.IsNullOrWhiteSpace(Username) && existingAdmin.Username != Username.Trim())
                {
                    existingAdmin.Username = Username.Trim();
                    hasChanges = true;
                }

                // Update password if provided
                if (wantsToChangePassword)
                {
                    existingAdmin.PasswordHash = HashPassword(NewPassword.Trim());
                    hasChanges = true;
                }

                if (hasChanges)
                {
                    _context.Entry(existingAdmin).State = EntityState.Modified;
                    var rowsAffected = await _context.SaveChangesAsync();

                    if (rowsAffected > 0)
                    {
                        TempData["ToastSuccess"] = "Profile updated successfully!";
                    }
                    else
                    {
                        TempData["ToastError"] = "Failed to save changes.";
                    }
                }
                else
                {
                    TempData["ToastError"] = "No changes were detected to save.";
                }
            }
            catch (DbUpdateException dbEx)
            {
                TempData["ToastError"] = $"Database update failed: {dbEx.InnerException?.Message ?? dbEx.Message}";
            }
            catch (Exception ex)
            {
                TempData["ToastError"] = $"An error occurred: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        private List<string> ValidatePasswordStrength(string password)
        {
            var errors = new List<string>();

            if (password.Length < 8)
                errors.Add("Password must be at least 8 characters long.");

            if (!password.Any(char.IsUpper))
                errors.Add("Password must contain at least one uppercase letter.");

            if (!password.Any(char.IsLower))
                errors.Add("Password must contain at least one lowercase letter.");

            if (!password.Any(char.IsDigit))
                errors.Add("Password must contain at least one number.");

            if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
                errors.Add("Password must contain at least one special character.");

            return errors;
        }
    }
}