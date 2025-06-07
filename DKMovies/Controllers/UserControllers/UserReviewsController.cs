using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

using System.IO;

using DKMovies.Models.Data.DatabaseModels;
using DKMovies.Models.Data;
using System.Security.Claims;

namespace DKMovies.Controllers
{
    public static class ProfanityFilter
    {
        private static List<string> _profanityList;
        private static readonly object _lock = new object();

        private static List<string> GetProfanityList()
        {
            if (_profanityList == null)
            {
                lock (_lock)
                {
                    if (_profanityList == null)
                    {
                        try
                        {
                            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "profanities.txt");
                            if (File.Exists(filePath))
                            {
                                _profanityList = File.ReadAllLines(filePath)
                                    .Where(line => !string.IsNullOrWhiteSpace(line))
                                    .Select(line => line.Trim().ToLower())
                                    .ToList();
                            }
                            else
                            {
                                _profanityList = new List<string>();
                            }
                        }
                        catch
                        {
                            _profanityList = new List<string>();
                        }
                    }
                }
            }
            return _profanityList;
        }

        public static bool ContainsProfanity(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            var profanityList = GetProfanityList();
            if (!profanityList.Any())
                return false;

            var cleanText = text.ToLower()
                .Replace("@", "a")
                .Replace("3", "e")
                .Replace("1", "i")
                .Replace("0", "o")
                .Replace("$", "s")
                .Replace("!", "i")
                .Replace("*", "")
                .Replace(" ", "")
                .Replace("-", "")
                .Replace("_", "");

            return profanityList.Any(profanity =>
                cleanText.Contains(profanity) ||
                text.ToLower().Contains(profanity));
        }
    }

    public class UserReviewsController : Controller
    {
        private readonly ApplicationDbContext _context;
        public UserReviewsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Reviews with filtering and sorting
        public async Task<IActionResult> Index(string sortOrder, int? ratingFilter)
        {
            var reviews = _context.Reviews
                .Include(r => r.Movie)
                .Include(r => r.User)
                .Include(r => r.ReviewReactions.Where(rr => rr.ReviewID == r.ID))
                .AsQueryable();

            // Apply rating filter
            if (ratingFilter.HasValue && ratingFilter.Value >= 1 && ratingFilter.Value <= 5)
            {
                reviews = reviews.Where(r => r.Rating == ratingFilter.Value);
            }
            // Apply sorting
            reviews = sortOrder switch
            {
                "date_desc" => reviews.OrderByDescending(r => r.CreatedAt),
                "date_asc" => reviews.OrderBy(r => r.CreatedAt),
                "rating_desc" => reviews.OrderByDescending(r => r.Rating),
                "rating_asc" => reviews.OrderBy(r => r.Rating),
                _ => reviews.OrderByDescending(r => r.CreatedAt) // Default: newest first
            };
            ViewBag.CurrentSort = sortOrder;
            ViewBag.CurrentRatingFilter = ratingFilter;
            ViewBag.DateSortParam = string.IsNullOrEmpty(sortOrder) ? "date_asc" : "";
            ViewBag.RatingSortParam = sortOrder == "rating_desc" ? "rating_asc" : "rating_desc";
            return View(await reviews.ToListAsync());
        }

        // AJAX endpoint for filtered reviews on movie details page
        [HttpGet]
        public async Task<IActionResult> GetFilteredReviews(int movieId, string sortOrder, int? ratingFilter, int skip = 0, int take = 8)
        {
            var reviews = _context.Reviews
                .Include(r => r.User)
                .Include(r => r.ReviewReactions.Where(rr => rr.ReviewID == r.ID))
                .Where(r => r.MovieID == movieId && r.IsApproved)
                .AsQueryable();
            // Apply rating filter
            if (ratingFilter.HasValue && ratingFilter.Value >= 1 && ratingFilter.Value <= 5)
            {
                reviews = reviews.Where(r => r.Rating == ratingFilter.Value);
            }
            // Apply sorting
            reviews = sortOrder switch
            {
                "date_desc" => reviews.OrderByDescending(r => r.CreatedAt),
                "date_asc" => reviews.OrderBy(r => r.CreatedAt),
                "rating_desc" => reviews.OrderByDescending(r => r.Rating),
                "rating_asc" => reviews.OrderBy(r => r.Rating),
                _ => reviews.OrderByDescending(r => r.CreatedAt)
            };

            var reviewsList = await reviews.Skip(skip).Take(take).ToListAsync();
            var totalCount = await reviews.CountAsync();

            // Get current user ID for ownership check
            var currentUserIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int? currentUserId = int.TryParse(currentUserIdStr, out int parsed) ? parsed : (int?)null;

            return Json(new
            {
                reviews = reviewsList.Select(r => new {
                    id = r.ID,
                    username = r.User?.Username,
                    profileImagePath = r.User?.ProfileImagePath,
                    rating = r.Rating,
                    comment = r.Comment,
                    createdAt = r.CreatedAt.ToString("d"),
                    userId = r.UserID,
                    canEdit = currentUserId == r.UserID,
                    // Add these new properties:
                    likeCount = r.ReviewReactions?.Count(rr => rr.IsLike) ?? 0,
                    dislikeCount = r.ReviewReactions?.Count(rr => !rr.IsLike) ?? 0,
                    userLiked = currentUserId.HasValue && r.ReviewReactions?.Any(rr => rr.UserID == currentUserId && rr.IsLike) == true,
                    userDisliked = currentUserId.HasValue && r.ReviewReactions?.Any(rr => rr.UserID == currentUserId && !rr.IsLike) == true
                }),
                hasMore = (skip + take) < totalCount
            });
        }

        [HttpGet]
        public IActionResult GetProfanityList()
        {
            try
            {
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "profanities.txt");
                if (System.IO.File.Exists(filePath))
                {
                    var profanities = System.IO.File.ReadAllLines(filePath)
                        .Where(line => !string.IsNullOrWhiteSpace(line))
                        .Select(line => line.Trim().ToLower())
                        .ToList();

                    return Json(profanities);
                }
                return Json(new List<string>());
            }
            catch
            {
                return Json(new List<string>());
            }
        }

        [HttpPost]
        public async Task<IActionResult> LeaveReview(int MovieID, int UserID, int Rating, string Comment)
        {
            // Get current user ID as int
            var currentUserIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(currentUserIdString, out int currentUserId))
            {
                TempData["ErrorMessage"] = "User authentication error.";
                return Redirect(Url.Action("Details", "UserMovies", new { id = MovieID }) + "#reviews");
            }

            // Validate that the UserID matches the current user (security check)
            if (UserID != currentUserId)
            {
                TempData["ErrorMessage"] = "Unauthorized action.";
                return Redirect(Url.Action("Details", "UserMovies", new { id = MovieID }) + "#reviews");
            }

            if (string.IsNullOrEmpty(Comment))
            {
                ViewBag.CommentError = "Comment is required.";
                return Redirect(Url.Action("Details", "UserMovies", new { id = MovieID }) + "#reviews");
            }

            // **ADDED: Check for profanity**
            if (ProfanityFilter.ContainsProfanity(Comment))
            {
                TempData["ErrorMessage"] = "Your review contains inappropriate language. Please revise your comment.";
                return Redirect(Url.Action("Details", "UserMovies", new { id = MovieID }) + "#reviews");
            }

            // Check if user already reviewed this movie
            var existingReview = await _context.Reviews
                .FirstOrDefaultAsync(r => r.MovieID == MovieID && r.UserID == UserID);

            if (existingReview != null)
            {
                TempData["ErrorMessage"] = "You have already reviewed this movie.";
                return Redirect(Url.Action("Details", "UserMovies", new { id = MovieID }) + "#reviews");
            }

            var review = new Review
            {
                MovieID = MovieID,
                UserID = UserID,
                Rating = Rating,
                Comment = Comment,
                CreatedAt = DateTime.Now,
                IsApproved = true // Auto-approve for now
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Review submitted successfully!";
            return Redirect(Url.Action("Details", "UserMovies", new { id = MovieID }) + "#reviews");
        }

        // **ADDED: Edit Review endpoint**
        [HttpGet]
        public async Task<IActionResult> EditReview(int id)
        {
            var currentUserIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(currentUserIdString, out int currentUserId))
            {
                return Json(new { success = false, message = "User authentication error." });
            }

            var review = await _context.Reviews.FirstOrDefaultAsync(r => r.ID == id);
            if (review == null)
            {
                return Json(new { success = false, message = "Review not found." });
            }

            // Check if user owns this review
            if (review.UserID != currentUserId)
            {
                return Json(new { success = false, message = "Unauthorized access to this review." });
            }

            return Json(new
            {
                success = true,
                review = new
                {
                    id = review.ID,
                    rating = review.Rating,
                    comment = review.Comment
                }
            });
        }

        // **ADDED: Update Review endpoint**
        [HttpPost]
        public async Task<IActionResult> UpdateReview(int id, int rating, string comment)
        {
            var currentUserIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(currentUserIdString, out int currentUserId))
            {
                return Json(new { success = false, message = "User authentication error." });
            }

            if (string.IsNullOrEmpty(comment))
            {
                return Json(new { success = false, message = "Comment is required." });
            }

            // **ADDED: Check for profanity**
            if (ProfanityFilter.ContainsProfanity(comment))
            {
                return Json(new { success = false, message = "Your review contains inappropriate language. Please revise your comment." });
            }

            var review = await _context.Reviews.FirstOrDefaultAsync(r => r.ID == id);
            if (review == null)
            {
                return Json(new { success = false, message = "Review not found." });
            }

            // Check if user owns this review
            if (review.UserID != currentUserId)
            {
                return Json(new { success = false, message = "Unauthorized access to this review." });
            }

            review.Rating = rating;
            review.Comment = comment;
            review.CreatedAt = DateTime.Now; // Update timestamp

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Review updated successfully!",
                review = new
                {
                    id = review.ID,
                    rating = review.Rating,
                    comment = review.Comment
                }
            });
        }

        // **ADDED: Delete Review endpoint**
        [HttpPost]
        public async Task<IActionResult> DeleteReview(int id)
        {
            var currentUserIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(currentUserIdString, out int currentUserId))
            {
                return Json(new { success = false, message = "User authentication error." });
            }

            var review = await _context.Reviews.FirstOrDefaultAsync(r => r.ID == id);
            if (review == null)
            {
                return Json(new { success = false, message = "Review not found." });
            }

            // Check if user owns this review
            if (review.UserID != currentUserId)
            {
                return Json(new { success = false, message = "Unauthorized access to this review." });
            }

            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Review deleted successfully!" });
        }

        [HttpPost]
        public async Task<IActionResult> ReactToReview(int reviewId, bool isLike)
        {
            var currentUserIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(currentUserIdString, out int currentUserId))
            {
                return Json(new { success = false, message = "User authentication error." });
            }

            var review = await _context.Reviews.FirstOrDefaultAsync(r => r.ID == reviewId);
            if (review == null)
            {
                return Json(new { success = false, message = "Review not found." });
            }

            // Check if user already reacted to this review
            var existingReaction = await _context.ReviewReactions
                .FirstOrDefaultAsync(rr => rr.ReviewID == reviewId && rr.UserID == currentUserId);

            if (existingReaction != null)
            {
                if (existingReaction.IsLike == isLike)
                {
                    // Remove reaction if clicking the same button
                    _context.ReviewReactions.Remove(existingReaction);
                }
                else
                {
                    // Update reaction if clicking different button
                    existingReaction.IsLike = isLike;
                    existingReaction.CreatedAt = DateTime.Now;
                }
            }
            else
            {
                // Create new reaction
                var newReaction = new ReviewReaction
                {
                    ReviewID = reviewId,
                    UserID = currentUserId,
                    IsLike = isLike,
                    CreatedAt = DateTime.Now
                };
                _context.ReviewReactions.Add(newReaction);
            }

            await _context.SaveChangesAsync();

            // Get updated counts and user's current reaction
            var likeCount = await _context.ReviewReactions.CountAsync(rr => rr.ReviewID == reviewId && rr.IsLike);
            var dislikeCount = await _context.ReviewReactions.CountAsync(rr => rr.ReviewID == reviewId && !rr.IsLike);
            var userReaction = await _context.ReviewReactions
                .FirstOrDefaultAsync(rr => rr.ReviewID == reviewId && rr.UserID == currentUserId);

            return Json(new
            {
                success = true,
                likeCount = likeCount,
                dislikeCount = dislikeCount,
                userLiked = userReaction?.IsLike == true,
                userDisliked = userReaction?.IsLike == false
            });
        }
    }
}