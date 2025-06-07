using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DKMovies.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

using DKMovies.Models.Data;
using DKMovies.Models.Data.DatabaseModels;

namespace Controllers.UserController
{
    [Authorize]
    public class UserMessagesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public UserMessagesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetUnreadMessageCount()
        {
            if (!User.Identity.IsAuthenticated)
                return Json(new { unreadCount = 0 });

            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            int unreadCount = 0;

            if (User.IsInRole("User"))
            {
                // For users: count unread messages FROM system/admin (IsFromUser = false)
                unreadCount = await _context.Messages
                    .CountAsync(m => m.UserID == userId && !m.IsFromUser && !m.IsRead && !m.IsDeletedByReceiver);
            }
            else if (User.IsInRole("Admin") || User.IsInRole("Staff"))
            {
                // For staff/admin: count unread messages FROM users (IsFromUser = true)
                // Fixed: Changed from "!m.IsFromUser == false" to "m.IsFromUser"
                unreadCount = await _context.Messages
                    .CountAsync(m => m.IsFromUser && !m.IsRead && !m.IsDeletedByReceiver);
            }

            return Json(new { unreadCount });
        }

        [HttpGet]
        public async Task<IActionResult> UserMessages()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var role = User.FindFirstValue(ClaimTypes.Role);

            if (string.IsNullOrEmpty(userIdClaim) || role != "User")
                return Unauthorized();

            int userId = int.Parse(userIdClaim);

            // Load messages for this user
            var messages = await _context.Messages
                .Where(m => m.UserID == userId)
                .OrderBy(m => m.SentAt)
                .ToListAsync();

            // Find the last read message from admin/system (IsFromUser = false)
            var lastReadAdminMessage = messages
                .Where(m => !m.IsFromUser && m.IsRead)
                .OrderByDescending(m => m.SentAt)
                .FirstOrDefault();

            // Mark all unread admin/system messages as read
            var unreadAdminMessages = messages
                .Where(m => !m.IsFromUser && !m.IsRead)
                .ToList();

            foreach (var message in unreadAdminMessages)
            {
                message.IsRead = true;
            }

            // Mark unread message notifications as read
            var unreadNotifications = await _context.Notifications
                .Where(n =>
                    n.UserID == userId &&
                    n.NotificationType == NotificationType.NewMessage.GetDisplayName() &&
                    !n.IsRead)
                .ToListAsync();

            foreach (var n in unreadNotifications)
                n.IsRead = true;

            // Save changes if there were any updates
            if (unreadAdminMessages.Any() || unreadNotifications.Any())
            {
                await _context.SaveChangesAsync();
            }

            // Pass the timestamp of the last read admin message to determine where to show the "new messages" line
            ViewBag.LastReadAdminMessageTime = lastReadAdminMessage?.SentAt;

            return View("UserMessages", messages);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UserSend(string messageText)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var role = User.FindFirstValue(ClaimTypes.Role);

            if (string.IsNullOrEmpty(userIdClaim) || role != "User")
                return Unauthorized();

            if (string.IsNullOrWhiteSpace(messageText))
                return RedirectToAction("UserMessages");

            int userId = int.Parse(userIdClaim);
            var user = await _context.Users.FindAsync(userId);

            if (user == null)
                return NotFound();

            var trimmedText = messageText.Trim();

            var message = new Message
            {
                UserID = userId,
                IsFromUser = true,
                MessageText = Encoding.UTF8.GetBytes(trimmedText),
                SentAt = DateTime.Now,
                IsRead = false,
                IsDeletedBySender = false,
                IsDeletedByReceiver = false
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            // Notify all admins - Fixed: Only set AdminID, not UserID
            var admins = await _context.Admins.ToListAsync();
            foreach (var admin in admins)
            {
                var notification = new Notification
                {
                    AdminID = admin.ID,
                    MessageID = message.ID, // Add this line to link the notification to the message
                    Title = $"New message from {user.Username}",
                    Message = trimmedText.Length > 100 ? trimmedText.Substring(0, 100) + "..." : trimmedText,
                    NotificationType = NotificationType.NewMessage.GetDisplayName(),
                    CreatedAt = DateTime.Now,
                    IsRead = false
                };

                _context.Notifications.Add(notification);
            }

            await _context.SaveChangesAsync();

            return RedirectToAction("UserMessages");
        }

        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> StaffMessages(int? userId)
        {
            var previews = await _context.Users
                .Select(u => new UserMessagePreview
                {
                    User = u,
                    LatestMessage = _context.Messages
                        .Where(m => m.UserID == u.ID)
                        .OrderByDescending(m => m.SentAt)
                        .Select(m => m.MessageText)
                        .FirstOrDefault(),
                    LatestTime = _context.Messages
                        .Where(m => m.UserID == u.ID)
                        .Max(m => (DateTime?)m.SentAt),
                    UnreadCount = _context.Messages
                        .Count(m => m.UserID == u.ID && m.IsFromUser && !m.IsRead && !m.IsDeletedByReceiver)
                })
                .OrderByDescending(p => p.LatestTime)
                .ToListAsync();

            List<Message> conversation = new();
            User? selectedUser = null;
            DateTime? lastReadUserMessageTime = null;

            if (userId.HasValue)
            {
                selectedUser = await _context.Users.FindAsync(userId);

                if (selectedUser != null)
                {
                    // Load conversation
                    conversation = await _context.Messages
                        .Where(m => m.UserID == userId)
                        .OrderBy(m => m.SentAt)
                        .ToListAsync();

                    // Find last read user message (IsFromUser = true)
                    lastReadUserMessageTime = conversation
                        .Where(m => m.IsFromUser && m.IsRead)
                        .OrderByDescending(m => m.SentAt)
                        .FirstOrDefault()?.SentAt;

                    // Mark unread user messages as read
                    var unreadUserMessages = conversation
                        .Where(m => m.IsFromUser && !m.IsRead)
                        .ToList();

                    foreach (var message in unreadUserMessages)
                    {
                        message.IsRead = true;
                    }

                    // Mark notifications as read
                    var currentAdminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (!string.IsNullOrEmpty(currentAdminId))
                    {
                        var staffNotifs = await _context.Notifications
                            .Where(n =>
                                n.NotificationType == NotificationType.NewMessage.GetDisplayName() &&
                                !n.IsRead &&
                                n.AdminID == int.Parse(currentAdminId))
                            .ToListAsync();

                        foreach (var notif in staffNotifs)
                            notif.IsRead = true;
                    }

                    if (unreadUserMessages.Any() || currentAdminId != null &&
                        await _context.Notifications.AnyAsync(n => n.AdminID == int.Parse(currentAdminId) && !n.IsRead))
                    {
                        await _context.SaveChangesAsync();
                    }
                }
            }

            ViewBag.CurrentUser = selectedUser;
            ViewBag.LastReadUserMessageTime = lastReadUserMessageTime;
            return View(Tuple.Create(previews, conversation));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> StaffSend(int userId, string messageText)
        {
            if (string.IsNullOrWhiteSpace(messageText))
                return RedirectToAction("StaffMessages", new { userId });

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound();

            var trimmedText = messageText.Trim();

            var message = new Message
            {
                UserID = userId,
                IsFromUser = false,
                MessageText = Encoding.UTF8.GetBytes(trimmedText),
                SentAt = DateTime.Now,
                IsRead = false,
                IsDeletedBySender = false,
                IsDeletedByReceiver = false
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            var notification = new Notification
            {
                UserID = userId,
                MessageID = message.ID, // Add this line to link the notification to the message
                Title = "You have a new message from DKMovies",
                Message = trimmedText.Length > 100 ? trimmedText.Substring(0, 100) + "..." : trimmedText,
                NotificationType = NotificationType.NewMessage.GetDisplayName(),
                CreatedAt = DateTime.Now,
                IsRead = false
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            return RedirectToAction("StaffMessages", new { userId });
        }
    }
}