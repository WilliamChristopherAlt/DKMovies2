using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using System.IO;

using DKMovies.Models.Data;
using DKMovies.Models.Data.DatabaseModels;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;

namespace Controllers.UserControllers
{
    [Authorize(Roles = "User")]
    public class UserProfileController : Controller
    {
        private readonly ApplicationDbContext _context;

        public UserProfileController(ApplicationDbContext context)
        {
            _context = context;
        }

        private void RemoveNavigationModelState()
        {
            ModelState.Remove(nameof(DKMovies.Models.Data.DatabaseModels.User.Tickets));
            ModelState.Remove(nameof(DKMovies.Models.Data.DatabaseModels.User.Reviews));
            ModelState.Remove(nameof(DKMovies.Models.Data.DatabaseModels.User.PasswordHash));
            ModelState.Remove(nameof(DKMovies.Models.Data.DatabaseModels.User.ProfileImagePath));
            ModelState.Remove(nameof(DKMovies.Models.Data.DatabaseModels.User.Notifications));
            ModelState.Remove(nameof(DKMovies.Models.Data.DatabaseModels.User.LoginAttempts));
            ModelState.Remove(nameof(DKMovies.Models.Data.DatabaseModels.User.ReviewReactions));
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

        private async Task<string?> SaveImageAsync(IFormFile image)
        {
            if (image == null || image.Length == 0)
                return null;

            // Validate file type
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var extension = Path.GetExtension(image.FileName).ToLower();
            if (!allowedExtensions.Contains(extension))
                return null;

            // Create directory if it doesn't exist
            var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "users");
            if (!Directory.Exists(uploadsDir))
                Directory.CreateDirectory(uploadsDir);

            // Generate unique filename
            var fileName = Guid.NewGuid().ToString("N") + extension;
            var filePath = Path.Combine(uploadsDir, fileName);

            // Save the file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await image.CopyToAsync(stream);
            }

            // Return just the filename (not full path)
            return fileName;
        }

        // GET: /Profile/Index
        public async Task<IActionResult> Index()
        {
            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
                return Unauthorized();

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound();

            ViewBag.ToastMessage = TempData["ToastSuccess"];
            ViewBag.ToastError = TempData["ToastError"];
            return View(user);
        }

        // POST: /Profile/Index (Update Profile)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(User updatedUser, string? NewPassword, IFormFile? ProfileImage)
        {
            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId) || userId != updatedUser.ID)
                return Unauthorized();

            RemoveNavigationModelState();

            // Check for duplicate username and email
            if (await _context.Users.AnyAsync(u => u.Username == updatedUser.Username && u.ID != updatedUser.ID))
                ModelState.AddModelError("Username", "Username already exists.");

            if (await _context.Users.AnyAsync(u => u.Email == updatedUser.Email && u.ID != updatedUser.ID))
                ModelState.AddModelError("Email", "Email is already in use.");

            if (!ModelState.IsValid)
            {
                // If validation fails, we need to reload the existing user to preserve the ProfileImagePath
                var existingUserForError = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.ID == updatedUser.ID);
                if (existingUserForError != null)
                {
                    updatedUser.ProfileImagePath = existingUserForError.ProfileImagePath;
                }
                return View(updatedUser);
            }

            try
            {
                // Get the existing user from database
                var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.ID == updatedUser.ID);
                if (existingUser == null)
                    return NotFound();

                // Store the current profile image path
                var currentImagePath = existingUser.ProfileImagePath;

                // Update properties
                existingUser.FullName = updatedUser.FullName?.Trim();
                existingUser.Phone = updatedUser.Phone?.Trim();
                existingUser.BirthDate = updatedUser.BirthDate;
                existingUser.Gender = updatedUser.Gender;
                existingUser.TwoFactorEnabled = updatedUser.TwoFactorEnabled;


                // Update password if provided
                // Update password if provided
                if (!string.IsNullOrWhiteSpace(NewPassword))
                {
                    if (string.IsNullOrWhiteSpace(Request.Form["CurrentPassword"]))
                    {
                        ModelState.AddModelError("CurrentPassword", "Current password is required to change password.");
                        // Reload the existing user to preserve the ProfileImagePath
                        updatedUser.ProfileImagePath = existingUser.ProfileImagePath;
                        return View(updatedUser);
                    }

                    if (!VerifyPassword(Request.Form["CurrentPassword"].ToString().Trim(), existingUser.PasswordHash))
                    {
                        ModelState.AddModelError("CurrentPassword", "Current password is incorrect.");
                        // Reload the existing user to preserve the ProfileImagePath
                        updatedUser.ProfileImagePath = existingUser.ProfileImagePath;
                        return View(updatedUser);
                    }

                    // Validate password strength
                    var passwordErrors = ValidatePasswordStrength(NewPassword.Trim());
                    if (passwordErrors.Any())
                    {
                        foreach (var error in passwordErrors)
                        {
                            ModelState.AddModelError("NewPassword", error);
                        }
                        // Reload the existing user to preserve the ProfileImagePath
                        updatedUser.ProfileImagePath = existingUser.ProfileImagePath;
                        return View(updatedUser);
                    }

                    existingUser.PasswordHash = HashPassword(NewPassword.Trim());
                }

                // Handle profile image upload
                if (ProfileImage != null && ProfileImage.Length > 0)
                {
                    var newImagePath = await SaveImageAsync(ProfileImage);
                    if (!string.IsNullOrEmpty(newImagePath))
                    {
                        existingUser.ProfileImagePath = newImagePath;

                        // Optionally delete the old image file
                        if (!string.IsNullOrEmpty(currentImagePath) && currentImagePath != "default.png")
                        {
                            var oldImagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "users", currentImagePath);
                            if (System.IO.File.Exists(oldImagePath))
                            {
                                System.IO.File.Delete(oldImagePath);
                            }
                        }
                    }
                }
                // If no new image is uploaded, keep the existing image path
                // (existingUser.ProfileImagePath already contains the current value)

                // Mark entity as modified explicitly
                _context.Entry(existingUser).State = EntityState.Modified;

                // Save changes
                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    TempData["ToastSuccess"] = $"Profile updated successfully!";
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