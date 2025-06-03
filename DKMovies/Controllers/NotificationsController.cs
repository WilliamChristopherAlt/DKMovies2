using DKMovies.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace MinimartWeb.Controllers
{
    [Authorize(Roles = "User, Admin, Staff")]
    public class NotificationsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public NotificationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!User.Identity.IsAuthenticated)
                return RedirectToAction("Login", "Account");

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var role = User.FindFirstValue(ClaimTypes.Role);

            List<Notification> notifications;

            if (role == "User")
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.ID == userId);
                if (user == null) return NotFound();

                notifications = await _context.Notifications
                    .Where(n => n.UserID == user.ID)
                    .OrderByDescending(n => n.CreatedAt)
                    .ToListAsync();
            }
            else if (role == "Admin" || role == "Staff")
            {
                var admin = await _context.Admins.FirstOrDefaultAsync(a => a.ID == userId);
                if (admin == null) return NotFound();

                notifications = await _context.Notifications
                    .Where(n => n.AdminID == admin.ID)
                    .OrderByDescending(n => n.CreatedAt)
                    .ToListAsync();
            }
            else
            {
                return Forbid();
            }

            return View(notifications);
        }

        // NotificationsController.cs - Add these methods/fixes

        [HttpGet]
        public async Task<IActionResult> GetLatestNotifications()
        {
            try
            {
                var userId = GetCurrentUserId();
                var userRole = GetCurrentUserRole();

                IQueryable<Notification> query;

                if (userRole == "Admin" || userRole == "Staff")
                {
                    query = _context.Notifications
                        .Where(n => n.AdminID == userId)
                        .OrderByDescending(n => n.CreatedAt)
                        .Take(10);
                }
                else
                {
                    query = _context.Notifications
                        .Where(n => n.UserID == userId)
                        .OrderByDescending(n => n.CreatedAt)
                        .Take(10);
                }

                var notifications = await query.ToListAsync();
                var unreadCount = notifications.Count(n => !n.IsRead);

                var result = new
                {
                    notifications = notifications.Select(n => new
                    {
                        id = n.ID,
                        title = n.Title,
                        message = n.Message,
                        createdAt = n.CreatedAt.ToString("MMM dd, HH:mm"),
                        isRead = n.IsRead,
                        notificationType = n.NotificationType, // This should be the enum string like "AccountRelated"
                        ticketID = n.TicketID
                    }).ToList(),
                    unreadCount = unreadCount
                };

                return Json(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetLatestNotifications: {ex.Message}");
                return Json(new { notifications = new List<object>(), unreadCount = 0 });
            }
        }

        [HttpPost]
        public async Task<IActionResult> MarkAsRead(int notificationId)
        {
            try
            {
                Console.WriteLine($"MarkAsRead called with notificationId: {notificationId}");

                var userId = GetCurrentUserId();
                var userRole = GetCurrentUserRole();

                Console.WriteLine($"Current user ID: {userId}, Role: {userRole}");

                Notification notification;

                if (userRole == "Admin" || userRole == "Staff")
                {
                    notification = await _context.Notifications
                        .FirstOrDefaultAsync(n => n.ID == notificationId && n.AdminID == userId);
                }
                else
                {
                    notification = await _context.Notifications
                        .FirstOrDefaultAsync(n => n.ID == notificationId && n.UserID == userId);
                }

                if (notification == null)
                {
                    Console.WriteLine($"Notification {notificationId} not found for user {userId}");
                    return NotFound("Notification not found");
                }

                Console.WriteLine($"Found notification: ID={notification.ID}, IsRead={notification.IsRead}, Title={notification.Title}");

                if (!notification.IsRead)
                {
                    notification.IsRead = true;
                    var saveResult = await _context.SaveChangesAsync();
                    Console.WriteLine($"SaveChanges result: {saveResult} rows affected");
                }
                else
                {
                    Console.WriteLine("Notification was already marked as read");
                }

                return Ok("Notification marked as read");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in MarkAsRead: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return BadRequest($"Failed to mark notification as read: {ex.Message}");
            }
        }

        // Helper methods - add these if you don't have them
        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : 0;
        }

        private string GetCurrentUserRole()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value ?? "User";
        }

        [HttpGet]
        public async Task<IActionResult> GetUnreadNotificationCount()
        {
            if (!User.Identity.IsAuthenticated)
                return Json(new { unreadCount = 0 });

            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            int unreadCount = 0;

            if (User.IsInRole("User"))
            {
                unreadCount = await _context.Notifications
                    .CountAsync(n => n.UserID == userId && !n.IsRead);
            }
            else if (User.IsInRole("Admin") || User.IsInRole("Staff"))
            {
                unreadCount = await _context.Notifications
                    .CountAsync(n => n.AdminID == userId && !n.IsRead);
            }

            return Json(new { unreadCount });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int notificationId)
        {
            var notification = await _context.Notifications.FindAsync(notificationId);
            if (notification == null) return NotFound();

            _context.Notifications.Remove(notification);
            await _context.SaveChangesAsync();

            return Json(new { message = "Notification deleted successfully." });
        }
    }
}
