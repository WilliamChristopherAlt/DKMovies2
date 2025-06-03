// MoviesListController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DKMovies.Models;
using System.Linq;
using System.Threading.Tasks;
using System.Drawing.Printing;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Filters;
using DKMovies.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using System.Data;
using Microsoft.Data.SqlClient;
using MathNet.Numerics.LinearAlgebra;

namespace DKMovies.Controllers
{
    public class LayoutDataFilter : IAsyncActionFilter
    {
        private readonly ApplicationDbContext _context;

        public LayoutDataFilter(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var controller = context.Controller as Controller;
            if (controller != null)
            {
                controller.ViewBag.LayoutGenres = await _context.Genres
                    .Where(g => g.MovieGenres.Any())
                    .OrderBy(g => g.Name)
                    .ToListAsync();

                controller.ViewBag.LayoutLanguages = await _context.Languages
                    .Where(l => l.Movies.Any())
                    .OrderBy(l => l.Name)
                    .ToListAsync();

                controller.ViewBag.LayoutCountries = await _context.Countries
                    .Where(c => c.Movies.Any())
                    .OrderBy(c => c.Name)
                    .ToListAsync();
            }

            await next();
        }
    }

    public class MoviesListController : Controller
    {
        private readonly ApplicationDbContext _context;
        private const int PageSize = 30;

        public MoviesListController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int page = 1)
        {
            // --- Hot Movies Based on Ticket Sales ---
            var hotMovieQuery = await _context.Tickets
                .Include(t => t.ShowTime)
                    .ThenInclude(s => s.Movie)
                .Where(t => t.ShowTime.Movie != null)
                .GroupBy(t => t.ShowTime.Movie)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .ToListAsync();

            var hotMovies = hotMovieQuery.Take(10).ToList();

            if (hotMovies.Count < 10)
            {
                var existingIds = hotMovies.Select(m => m.ID).ToHashSet();
                var recentMovies = await _context.Movies
                    .Where(m => !existingIds.Contains(m.ID))
                    .OrderByDescending(m => m.ReleaseDate)
                    .Take(10 - hotMovies.Count)
                    .ToListAsync();
                hotMovies.AddRange(recentMovies);
            }

            foreach (var movie in hotMovies)
            {
                if (string.IsNullOrEmpty(movie.PosterImagePath))
                {
                    movie.PosterImagePath = "default.jpg";
                }
                else if (!movie.PosterImagePath.StartsWith("assets/images/movie_posters/"))
                {
                    movie.PosterImagePath = Path.GetFileName(movie.PosterImagePath);
                }
            }

            ViewBag.HotMovies = hotMovies;

            // --- Recommended Movies using Matrix Factorization ---
            List<Movie> recommendedMovies;
            if (User.Identity.IsAuthenticated && User.IsInRole("User"))
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                recommendedMovies = await GetRecommendedMovies(userId);
            }
            else
            {
                recommendedMovies = await _context.Movies
                    .OrderByDescending(m => m.ReleaseDate)
                    .Take(10)
                    .ToListAsync();
            }

            foreach (var movie in recommendedMovies)
            {
                if (string.IsNullOrEmpty(movie.PosterImagePath))
                {
                    movie.PosterImagePath = "default.jpg";
                }
                else if (!movie.PosterImagePath.StartsWith("assets/images/movie_posters/"))
                {
                    movie.PosterImagePath = Path.GetFileName(movie.PosterImagePath);
                }
            }

            ViewBag.RecommendedMovies = recommendedMovies;

            // --- Movie List Pagination ---
            var totalMovies = await _context.Movies.CountAsync();
            var totalPages = (int)Math.Ceiling(totalMovies / (double)PageSize);

            var movies = await _context.Movies
                .Include(m => m.Country)
                .Include(m => m.Language)
                .Include(m => m.Rating)
                .Include(m => m.Director)
                .Include(m => m.MovieGenres).ThenInclude(mg => mg.Genre)
                .OrderBy(m => m.Title)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            var movieIds = movies.Select(m => m.ID).ToList();
            var avgRatings = await _context.Reviews
                .Where(r => movieIds.Contains(r.MovieID) && r.IsApproved)
                .GroupBy(r => r.MovieID)
                .Select(g => new { g.Key, AvgRating = g.Average(r => r.Rating) })
                .ToListAsync();

            ViewData["AverageRatings"] = avgRatings.ToDictionary(x => x.Key, x => x.AvgRating);
            ViewData["CurrentPage"] = page;
            ViewData["TotalPages"] = totalPages;

            return View(movies);
        }

        private async Task<List<Movie>> GetRecommendedMovies(int userId)
        {
            try
            {
                var reviews = await _context.Reviews
                    .Where(r => r.IsApproved)
                    .ToListAsync();

                var users = reviews.Select(r => r.UserID).Distinct().ToList();
                var movies = await _context.Movies.Select(m => m.ID).ToListAsync();

                var userIndex = users.Select((u, i) => new { u, i }).ToDictionary(x => x.u, x => x.i);
                var movieIndex = movies.Select((m, i) => new { m, i }).ToDictionary(x => x.m, x => x.i);

                var ratingsMatrix = Matrix<double>.Build.Dense(users.Count, movies.Count, 0);

                foreach (var review in reviews)
                {
                    int ui = userIndex[review.UserID];
                    int mi = movieIndex[review.MovieID];
                    ratingsMatrix[ui, mi] = review.Rating;
                }

                var userRatings = ratingsMatrix.Row(userIndex[userId]);

                int latentFeatures = 10;
                var rand = new Random();

                var P = Matrix<double>.Build.Random(users.Count, latentFeatures);
                var Q = Matrix<double>.Build.Random(movies.Count, latentFeatures);

                double alpha = 0.005;
                double beta = 0.02;
                int iterations = 100;

                for (int step = 0; step < iterations; step++)
                {
                    for (int u = 0; u < users.Count; u++)
                    {
                        for (int m = 0; m < movies.Count; m++)
                        {
                            if (ratingsMatrix[u, m] > 0)
                            {
                                double e = ratingsMatrix[u, m] - P.Row(u) * Q.Row(m).ToColumnMatrix().Column(0);
                                for (int k = 0; k < latentFeatures; k++)
                                {
                                    P[u, k] += alpha * (2 * e * Q[m, k] - beta * P[u, k]);
                                    Q[m, k] += alpha * (2 * e * P[u, k] - beta * Q[m, k]);
                                }
                            }
                        }
                    }
                }

                var predicted = P * Q.Transpose();
                var predictedRatings = predicted.Row(userIndex[userId]);

                var userRatedMovieIds = reviews
                    .Where(r => r.UserID == userId)
                    .Select(r => r.MovieID)
                    .ToHashSet();

                var recommendations = predictedRatings
                    .Select((score, idx) => new { MovieID = movies[idx], Score = score })
                    .Where(x => !userRatedMovieIds.Contains(x.MovieID))
                    .OrderByDescending(x => x.Score)
                    .Take(10)
                    .Select(x => x.MovieID)
                    .ToList();

                return await _context.Movies
                    .Where(m => recommendations.Contains(m.ID))
                    .ToListAsync();
            }
            catch
            {
                return await _context.Movies
                    .OrderByDescending(m => m.ReleaseDate)
                    .Take(10)
                    .ToListAsync();
            }
        }
        private double CalculatePearsonSimilarity(Dictionary<int, double> user1Ratings, Dictionary<int, double> user2Ratings, List<int> commonMovies)
        {
            if (commonMovies.Count < 2) return 0;

            var sum1 = commonMovies.Sum(m => user1Ratings[m]);
            var sum2 = commonMovies.Sum(m => user2Ratings[m]);
            var sum1Sq = commonMovies.Sum(m => Math.Pow(user1Ratings[m], 2));
            var sum2Sq = commonMovies.Sum(m => Math.Pow(user2Ratings[m], 2));
            var pSum = commonMovies.Sum(m => user1Ratings[m] * user2Ratings[m]);

            var num = pSum - (sum1 * sum2 / commonMovies.Count);
            var den = Math.Sqrt((sum1Sq - Math.Pow(sum1, 2) / commonMovies.Count) * (sum2Sq - Math.Pow(sum2, 2) / commonMovies.Count));

            return den == 0 ? 0 : num / den;
        }

        public async Task<IActionResult> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return RedirectToAction(nameof(Index));

            query = query.ToLower();

            var movies = await _context.Movies
                .Include(m => m.Country)
                .Include(m => m.Language)
                .Include(m => m.Rating)
                .Include(m => m.Director)
                .Include(m => m.MovieGenres)
                    .ThenInclude(mg => mg.Genre)
                .Where(m =>
                    EF.Functions.Like(m.Title.ToLower(), $"%{query}%") ||
                    EF.Functions.Like(m.Description.ToLower(), $"%{query}%") ||
                    (m.Director != null && EF.Functions.Like(m.Director.FullName.ToLower(), $"%{query}%")) ||
                    (m.Country != null && EF.Functions.Like(m.Country.Name.ToLower(), $"%{query}%")) ||
                    (m.Language != null && EF.Functions.Like(m.Language.Name.ToLower(), $"%{query}%")) ||
                    m.MovieGenres.Any(mg => EF.Functions.Like(mg.Genre.Name.ToLower(), $"%{query}%"))
                )
                .ToListAsync();

            ViewData["Title"] = $"Search Results for \"{query}\"";
            return View("Index", movies);
        }

        [HttpGet]
        public async Task<IActionResult> AdvancedSearch()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Results(SearchModel model, int page = 1)
        {
            var query = _context.Movies
                .Include(m => m.Country)
                .Include(m => m.Language)
                .Include(m => m.Rating)
                .Include(m => m.Director)
                .Include(m => m.MovieGenres).ThenInclude(mg => mg.Genre)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(model.Title))
                query = query.Where(m => m.Title.Contains(model.Title));
            if (!string.IsNullOrWhiteSpace(model.Director))
                query = query.Where(m => m.Director != null && m.Director.FullName.Contains(model.Director));
            if (model.GenreId.HasValue)
                query = query.Where(m => m.MovieGenres.Any(mg => mg.GenreID == model.GenreId));
            if (model.LanguageId.HasValue)
                query = query.Where(m => m.LanguageID == model.LanguageId);
            if (model.CountryId.HasValue)
                query = query.Where(m => m.CountryID == model.CountryId);
            if (model.ReleaseFrom.HasValue)
                query = query.Where(m => m.ReleaseDate >= model.ReleaseFrom);
            if (model.ReleaseTo.HasValue)
                query = query.Where(m => m.ReleaseDate <= model.ReleaseTo);

            query = model.Sort switch
            {
                "date_asc" => query.OrderBy(m => m.ReleaseDate),
                "date_desc" => query.OrderByDescending(m => m.ReleaseDate),
                "title_desc" => query.OrderByDescending(m => m.Title),
                _ => query.OrderBy(m => m.Title)
            };

            var totalMovies = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalMovies / (double)PageSize);

            var movies = await query
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            var movieIds = movies.Select(m => m.ID).ToList();
            var avgRatings = await _context.Reviews
                .Where(r => movieIds.Contains(r.MovieID) && r.IsApproved)
                .GroupBy(r => r.MovieID)
                .Select(g => new { MovieID = g.Key, AvgRating = g.Average(r => r.Rating) })
                .ToListAsync();

            ViewData["AverageRatings"] = avgRatings.ToDictionary(x => x.MovieID, x => x.AvgRating);
            ViewData["CurrentPage"] = page;
            ViewData["TotalPages"] = totalPages;

            ViewBag.SearchModel = model;

            return View("Index", movies);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var movie = await _context.Movies
                .Include(m => m.Country)
                .Include(m => m.Language)
                .Include(m => m.Rating)
                .Include(m => m.Director)
                .Include(m => m.MovieGenres)
                    .ThenInclude(mg => mg.Genre)
                .Include(m => m.Reviews)
                    .ThenInclude(r => r.User)
                .FirstOrDefaultAsync(m => m.ID == id);

            if (movie == null)
                return NotFound();

            var averageRating = movie.Reviews.Any() ? movie.Reviews.Average(r => r.Rating) : 0;
            ViewData["AverageRating"] = averageRating;

            bool isInWatchlist = false;
            if (User.Identity.IsAuthenticated && User.IsInRole("User"))
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                isInWatchlist = await _context.WatchList
                    .AnyAsync(w => w.UserID == userId && w.MovieID == id);
            }

            ViewData["IsInWatchlist"] = isInWatchlist;

            bool hasUserReviewed = false;

            if (User.Identity?.IsAuthenticated == true && User.IsInRole("User"))
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                hasUserReviewed = await _context.Reviews
                    .AnyAsync(r => r.MovieID == id && r.UserID == userId);
            }

            ViewData["HasUserReviewed"] = hasUserReviewed;

            return View(movie);
        }

        public IActionResult OrderTicket(int id, string search, string date, int? theaterId)
        {
            var movie = _context.Movies.Find(id);
            if (movie == null) return NotFound();

            var now = DateTime.Now;

            var allShowtimes = _context.ShowTimes
                .Include(s => s.Auditorium)
                    .ThenInclude(a => a.Theater)
                .Where(s => s.MovieID == id && s.StartTime >= now)
                .ToList();

            ViewData["Movie"] = movie;
            ViewData["Search"] = search;
            ViewData["Date"] = date;
            ViewData["SelectedTheaterId"] = theaterId;

            return View(allShowtimes);
        }

        public IActionResult OrderTicketDetails(int id)
        {
            var showtime = _context.ShowTimes
                .Include(s => s.Movie)
                .Include(s => s.Auditorium)
                    .ThenInclude(a => a.Seats)
                .FirstOrDefault(s => s.ID == id);

            if (showtime == null)
            {
                TempData["Error"] = "Showtime not found.";
                return RedirectToAction("OrderTicket", new { id });
            }

            if (showtime.Auditorium == null)
            {
                TempData["Error"] = "Auditorium information is missing.";
                return RedirectToAction("OrderTicket", new { id });
            }

            var seats = showtime.Auditorium.Seats?.ToList() ?? new List<Seat>();

            var takenSeats = _context.TicketSeats
                .Include(ts => ts.Ticket)
                .Where(ts => ts.Ticket != null && ts.Ticket.ShowTimeID == id)
                .Select(ts => ts.SeatID)
                .ToList();

            ViewData["TakenSeats"] = takenSeats;
            ViewData["ShowTime"] = showtime;

            return View(seats);
        }

        [Authorize(Roles = "User")]
        public IActionResult ConfirmOrder(int ShowTimeID, List<int> SelectedSeats)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                TempData["Error"] = "User session is invalid.";
                return RedirectToAction("Login", "Account");
            }

            var showTime = _context.ShowTimes
                .Include(st => st.Tickets)
                    .ThenInclude(t => t.TicketSeats)
                .FirstOrDefault(st => st.ID == ShowTimeID);

            if (showTime == null)
            {
                return NotFound("ShowTime not found.");
            }

            var availableSeats = _context.Seats
                .Where(s => SelectedSeats.Contains(s.ID))
                .ToList();

            var takenSeatIds = showTime.Tickets
                .SelectMany(t => t.TicketSeats)
                .Select(ts => ts.SeatID)
                .ToHashSet();

            var alreadyTaken = SelectedSeats.Intersect(takenSeatIds).ToList();
            if (alreadyTaken.Any())
            {
                TempData["Error"] = "Some seats have already been booked. Please try again.";
                return RedirectToAction("OrderTicketDetails", new { id = ShowTimeID });
            }

            var ticket = new Ticket
            {
                UserID = userId,
                ShowTimeID = ShowTimeID,
                PurchaseTime = DateTime.Now,
            };

            _context.Tickets.Add(ticket);
            _context.SaveChanges();

            var ticketSeats = availableSeats.Select(seat => new TicketSeat
            {
                TicketID = ticket.ID,
                SeatID = seat.ID
            }).ToList();

            _context.TicketSeats.AddRange(ticketSeats);
            _context.SaveChanges();

            TempData["Success"] = "Seats reserved successfully!";
            return RedirectToAction("OrderConfirmation", new { ticketId = ticket.ID });
        }

        public IActionResult OrderConfirmation(int ticketId)
        {
            var ticket = _context.Tickets
                .Include(t => t.ShowTime)
                    .ThenInclude(st => st.Movie)
                .Include(t => t.ShowTime)
                    .ThenInclude(st => st.Auditorium)
                        .ThenInclude(a => a.Theater)
                .Include(t => t.TicketSeats)
                    .ThenInclude(ts => ts.Seat)
                .FirstOrDefault(t => t.ID == ticketId);

            if (ticket == null)
            {
                return NotFound("Ticket not found.");
            }

            return View(ticket);
        }
    }
}