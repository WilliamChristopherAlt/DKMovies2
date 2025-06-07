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

namespace Controllers.UserController
{
    [Authorize(Roles = "User")]
    public class UsersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public UsersController(ApplicationDbContext context)
        {
            _context = context;
        }

        private void RemoveNavigationModelState()
        {
            ModelState.Remove(nameof(DKMovies.Models.Data.DatabaseModels.User.Tickets));
            ModelState.Remove(nameof(DKMovies.Models.Data.DatabaseModels.User.Reviews));
            ModelState.Remove(nameof(DKMovies.Models.Data.DatabaseModels.User.PasswordHash));
        }

        private string HashPassword(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(bytes);
            }
        }

        private async Task<string?> SaveImageAsync(IFormFile image)
        {
            if (image == null || image.Length == 0)
                return null;

            var uploads = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "users");
            Directory.CreateDirectory(uploads);
            var fileName = Guid.NewGuid().ToString("N") + Path.GetExtension(image.FileName);
            var path = Path.Combine(uploads, fileName);

            using (var stream = new FileStream(path, FileMode.Create))
            {
                await image.CopyToAsync(stream);
            }

            return fileName;
        }

        // GET: /Users/Index
        public async Task<IActionResult> Index()
        {
            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
                return Unauthorized();

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound();

            ViewBag.ProfileImagePath = TempData["ProfileImagePath"] ?? user?.ProfileImagePath ?? "default.png";
            ViewBag.ToastMessage = TempData["ToastSuccess"];
            return View(user);

        }

        // POST: /Users/Index (Update Profile)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(User updatedUser, string? NewPassword, IFormFile? ProfileImage)
        {
            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId) || userId != updatedUser.ID)
                return Unauthorized();

            RemoveNavigationModelState();

            if (await _context.Users.AnyAsync(u => u.Username == updatedUser.Username && u.ID != updatedUser.ID))
                ModelState.AddModelError("Username", "Tên đăng nhập đã tồn tại.");

            if (await _context.Users.AnyAsync(u => u.Email == updatedUser.Email && u.ID != updatedUser.ID))
                ModelState.AddModelError("Email", "Email đã được sử dụng.");

            if (!ModelState.IsValid)
                return View(updatedUser);

            // ✅ Lấy user hiện tại từ database để cập nhật
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.ID == updatedUser.ID);
            if (existingUser == null)
                return NotFound();

            // ✅ Cập nhật TRỰC TIẾP trên existingUser thay vì tạo object mới
            existingUser.FullName = updatedUser.FullName;
            existingUser.Username = updatedUser.Username;
            existingUser.Email = updatedUser.Email;
            existingUser.Phone = updatedUser.Phone;
            existingUser.BirthDate = updatedUser.BirthDate;
            existingUser.Gender = updatedUser.Gender;

            // ✅ QUAN TRỌNG: Cập nhật TwoFactorEnabled từ form
            existingUser.TwoFactorEnabled = updatedUser.TwoFactorEnabled;

            // ✅ Cập nhật mật khẩu nếu có
            if (!string.IsNullOrWhiteSpace(NewPassword))
            {
                existingUser.PasswordHash = HashPassword(NewPassword);
            }

            // ✅ Xử lý ảnh đại diện
            if (ProfileImage != null)
            {
                var newImagePath = await SaveImageAsync(ProfileImage);
                if (!string.IsNullOrEmpty(newImagePath))
                    existingUser.ProfileImagePath = newImagePath;
            }

            // ✅ Debug logging (có thể xóa sau khi test xong)
            Console.WriteLine($"TwoFactorEnabled sẽ được lưu: {existingUser.TwoFactorEnabled}");
            Console.WriteLine($"EmailConfirmed: {existingUser.EmailConfirmed}");

            try
            {
                // ✅ Lưu thay đổi
                await _context.SaveChangesAsync();

                TempData["ProfileImagePath"] = existingUser.ProfileImagePath ?? "default.png";
                TempData["ToastSuccess"] = "✅ Thông tin đã được cập nhật.";

                Console.WriteLine($"Đã lưu thành công. TwoFactorEnabled: {existingUser.TwoFactorEnabled}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi lưu: {ex.Message}");
                TempData["ToastError"] = "❌ Có lỗi xảy ra khi lưu thông tin.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
