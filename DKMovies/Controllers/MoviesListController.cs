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
using System.Text.Json;

using MathNet.Numerics.LinearAlgebra;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace DKMovies.Controllers
{
    public class MovieFeatures
    {
        public int MovieId { get; set; }
        public string Title { get; set; }
        public string CombinedFeatures { get; set; }
    }

    public class MovieVectorData
    {
        public Dictionary<int, float[]> MovieVectors { get; set; }
        public DateTime LastUpdated { get; set; }
    }

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
        private readonly IWebHostEnvironment _webHostEnvironment;
        private const int PageSize = 30;

        // TEST VARIABLE - Set to true to recompute matrix, false to use cached version
        //private const bool RECOMPUTE_MATRIX = true;
        private const bool RECOMPUTE_MATRIX = false;

        private readonly string _matrixFilePath;

        public MoviesListController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _matrixFilePath = Path.Combine(_webHostEnvironment.WebRootPath, "movie_vectors.json");
        }

        private List<MovieFeatures> PrepareMovieData()
        {
            var movies = _context.Movies
                .Include(m => m.Director)
                .Include(m => m.MovieGenres).ThenInclude(mg => mg.Genre)
                .ToList();

            var movieFeaturesList = movies.Select(m => new MovieFeatures
            {
                MovieId = m.ID,
                Title = m.Title,
                // Improved feature combination with better text preprocessing
                CombinedFeatures = string.Join(" ", new[]
                {
            m.Title?.ToLower().Trim() ?? "",
            m.Description?.ToLower().Trim() ?? "",
            m.Director?.FullName?.ToLower().Trim() ?? "",
            string.Join(" ", m.MovieGenres.Select(g => g.Genre.Name?.ToLower().Trim() ?? ""))
        }.Where(s => !string.IsNullOrWhiteSpace(s)))
            }).Where(mf => !string.IsNullOrWhiteSpace(mf.CombinedFeatures))
              .ToList();

            return movieFeaturesList;
        }

        private Dictionary<int, float[]> ComputeTfIdfVectors(List<MovieFeatures> movieFeaturesList)
        {
            try
            {
                var mlContext = new MLContext(seed: 1); // Fixed seed for consistency

                if (!movieFeaturesList.Any())
                    return new Dictionary<int, float[]>();

                var data = mlContext.Data.LoadFromEnumerable(movieFeaturesList);

                // Improved text featurization pipeline
                var pipeline = mlContext.Transforms.Text.NormalizeText(
                        outputColumnName: "NormalizedText",
                        inputColumnName: nameof(MovieFeatures.CombinedFeatures),
                        caseMode: Microsoft.ML.Transforms.Text.TextNormalizingEstimator.CaseMode.Lower,
                        keepDiacritics: false,
                        keepPunctuations: false,
                        keepNumbers: true)
                    .Append(mlContext.Transforms.Text.TokenizeIntoWords(
                        outputColumnName: "Tokens",
                        inputColumnName: "NormalizedText"))
                    .Append(mlContext.Transforms.Text.RemoveDefaultStopWords(
                        outputColumnName: "TokensWithoutStopWords",
                        inputColumnName: "Tokens"))
                    .Append(mlContext.Transforms.Text.FeaturizeText(
                        outputColumnName: "Features",
                        inputColumnName: "TokensWithoutStopWords"));

                var model = pipeline.Fit(data);
                var transformedData = model.Transform(data);

                // Get the feature vectors
                var featuresColumn = transformedData.GetColumn<float[]>("Features").ToArray();
                var movieIds = transformedData.GetColumn<int>(nameof(MovieFeatures.MovieId)).ToArray();

                var movieVectors = new Dictionary<int, float[]>();
                for (int i = 0; i < movieIds.Length; i++)
                {
                    if (featuresColumn[i] != null && featuresColumn[i].Length > 0)
                    {
                        movieVectors[movieIds[i]] = featuresColumn[i];
                    }
                }

                return movieVectors;
            }
            catch (Exception ex)
            {
                // Log the exception if you have logging
                System.Diagnostics.Debug.WriteLine($"Error computing TF-IDF vectors: {ex.Message}");
                return new Dictionary<int, float[]>();
            }
        }

        private async Task<Dictionary<int, float[]>> GetOrComputeMovieVectors()
        {
            try
            {
                // Check if we should recompute or if cached file doesn't exist
                if (RECOMPUTE_MATRIX || !System.IO.File.Exists(_matrixFilePath))
                {
                    System.Diagnostics.Debug.WriteLine("Computing new TF-IDF matrix...");

                    var movieFeaturesList = PrepareMovieData();
                    var movieVectors = ComputeTfIdfVectors(movieFeaturesList);

                    // Save to file
                    await SaveMovieVectorsToFile(movieVectors);

                    System.Diagnostics.Debug.WriteLine($"TF-IDF matrix computed and saved. Total vectors: {movieVectors.Count}");
                    return movieVectors;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Loading cached TF-IDF matrix...");
                    return await LoadMovieVectorsFromFile();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetOrComputeMovieVectors: {ex.Message}");
                return new Dictionary<int, float[]>();
            }
        }

        private async Task SaveMovieVectorsToFile(Dictionary<int, float[]> movieVectors)
        {
            try
            {
                // Ensure the wwwroot directory exists
                var wwwrootPath = _webHostEnvironment.WebRootPath;
                if (!Directory.Exists(wwwrootPath))
                {
                    Directory.CreateDirectory(wwwrootPath);
                }

                var vectorData = new MovieVectorData
                {
                    MovieVectors = movieVectors,
                    LastUpdated = DateTime.UtcNow
                };

                var options = new JsonSerializerOptions
                {
                    WriteIndented = false // Compact JSON to save space
                };

                var json = JsonSerializer.Serialize(vectorData, options);
                await System.IO.File.WriteAllTextAsync(_matrixFilePath, json);

                System.Diagnostics.Debug.WriteLine($"Movie vectors saved to: {_matrixFilePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving movie vectors to file: {ex.Message}");
            }
        }

        private async Task<Dictionary<int, float[]>> LoadMovieVectorsFromFile()
        {
            try
            {
                if (!System.IO.File.Exists(_matrixFilePath))
                {
                    System.Diagnostics.Debug.WriteLine("Matrix file does not exist, will compute new one.");
                    return new Dictionary<int, float[]>();
                }

                var json = await System.IO.File.ReadAllTextAsync(_matrixFilePath);
                var vectorData = JsonSerializer.Deserialize<MovieVectorData>(json);

                if (vectorData?.MovieVectors != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Loaded {vectorData.MovieVectors.Count} movie vectors from cache (Last updated: {vectorData.LastUpdated})");
                    return vectorData.MovieVectors;
                }

                return new Dictionary<int, float[]>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading movie vectors from file: {ex.Message}");
                return new Dictionary<int, float[]>();
            }
        }

        private float ComputeCosineSimilarity(float[] vectorA, float[] vectorB)
        {
            if (vectorA == null || vectorB == null || vectorA.Length != vectorB.Length || vectorA.Length == 0)
                return 0;

            float dotProduct = 0;
            float magnitudeA = 0;
            float magnitudeB = 0;

            for (int i = 0; i < vectorA.Length; i++)
            {
                dotProduct += vectorA[i] * vectorB[i];
                magnitudeA += vectorA[i] * vectorA[i];
                magnitudeB += vectorB[i] * vectorB[i];
            }

            magnitudeA = (float)Math.Sqrt(magnitudeA);
            magnitudeB = (float)Math.Sqrt(magnitudeB);

            if (magnitudeA == 0 || magnitudeB == 0)
                return 0;

            return dotProduct / (magnitudeA * magnitudeB);
        }

        private async Task<List<Movie>> GetSimilarMovies(int movieId, Dictionary<int, float[]> movieVectors, int topN = 10)
        {
            try
            {
                if (!movieVectors.ContainsKey(movieId) || movieVectors.Count <= 1)
                    return new List<Movie>();

                var targetVector = movieVectors[movieId];
                var similarityScores = new List<(int MovieId, float Score)>();

                foreach (var kvp in movieVectors)
                {
                    if (kvp.Key == movieId)
                        continue;

                    var score = ComputeCosineSimilarity(targetVector, kvp.Value);
                    if (score > 0) // Only include movies with positive similarity
                    {
                        similarityScores.Add((kvp.Key, score));
                    }
                }

                if (!similarityScores.Any())
                {
                    // Fallback: return movies from same genre
                    var currentMovie = await _context.Movies
                        .Include(m => m.MovieGenres).ThenInclude(mg => mg.Genre)
                        .FirstOrDefaultAsync(m => m.ID == movieId);

                    if (currentMovie?.MovieGenres?.Any() == true)
                    {
                        var genreIds = currentMovie.MovieGenres.Select(mg => mg.GenreID).ToList();
                        return await _context.Movies
                            .Include(m => m.MovieGenres)
                            .Where(m => m.ID != movieId && m.MovieGenres.Any(mg => genreIds.Contains(mg.GenreID)))
                            .Take(topN)
                            .ToListAsync();
                    }

                    // Final fallback: return recent movies
                    return await _context.Movies
                        .Where(m => m.ID != movieId)
                        .OrderByDescending(m => m.ReleaseDate)
                        .Take(topN)
                        .ToListAsync();
                }

                var topSimilarMovieIds = similarityScores
                    .OrderByDescending(s => s.Score)
                    .Take(topN)
                    .Select(s => s.MovieId)
                    .ToList();

                var similarMovies = await _context.Movies
                    .Where(m => topSimilarMovieIds.Contains(m.ID))
                    .ToListAsync();

                // Maintain the order from similarity scores
                var orderedSimilarMovies = topSimilarMovieIds
                    .Select(id => similarMovies.FirstOrDefault(m => m.ID == id))
                    .Where(m => m != null)
                    .ToList();

                return orderedSimilarMovies;
            }
            catch (Exception ex)
            {
                // Log the exception if you have logging
                System.Diagnostics.Debug.WriteLine($"Error getting similar movies: {ex.Message}");

                // Fallback to genre-based similarity
                var currentMovie = await _context.Movies
                    .Include(m => m.MovieGenres).ThenInclude(mg => mg.Genre)
                    .FirstOrDefaultAsync(m => m.ID == movieId);

                if (currentMovie?.MovieGenres?.Any() == true)
                {
                    var genreIds = currentMovie.MovieGenres.Select(mg => mg.GenreID).ToList();
                    return await _context.Movies
                        .Include(m => m.MovieGenres)
                        .Where(m => m.ID != movieId && m.MovieGenres.Any(mg => genreIds.Contains(mg.GenreID)))
                        .Take(topN)
                        .ToListAsync();
                }

                return new List<Movie>();
            }
        }

        public async Task<IActionResult> Index(int page = 1)
        {
            // Pre-compute and cache movie vectors if needed
            await GetOrComputeMovieVectors();

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
                .Include(m => m.MovieGenres).ThenInclude(mg => mg.Genre)
                .Include(m => m.Reviews.Where(r => r.IsApproved)).ThenInclude(r => r.User)
                .FirstOrDefaultAsync(m => m.ID == id);

            if (movie == null)
                return NotFound();

            // Get similar movies using cached TF-IDF vectors
            List<Movie> similarMovies = new List<Movie>();
            try
            {
                var movieVectors = await LoadMovieVectorsFromFile();
                if (movieVectors.Any())
                {
                    similarMovies = await GetSimilarMovies(movie.ID, movieVectors, 8); // Get 8 similar movies
                }
            }
            catch (Exception ex)
            {
                // Log the exception if you have logging
                System.Diagnostics.Debug.WriteLine($"Error in similarity computation: {ex.Message}");
            }

            // If no similar movies found, fallback to genre-based recommendations
            if (!similarMovies.Any() && movie.MovieGenres.Any())
            {
                var genreIds = movie.MovieGenres.Select(mg => mg.GenreID).ToList();
                similarMovies = await _context.Movies
                    .Include(m => m.MovieGenres)
                    .Where(m => m.ID != movie.ID && m.MovieGenres.Any(mg => genreIds.Contains(mg.GenreID)))
                    .OrderByDescending(m => m.ReleaseDate)
                    .Take(8)
                    .ToListAsync();
            }

            // Final fallback - recent movies from same country or language
            if (!similarMovies.Any())
            {
                similarMovies = await _context.Movies
                    .Where(m => m.ID != movie.ID &&
                               (m.CountryID == movie.CountryID || m.LanguageID == movie.LanguageID))
                    .OrderByDescending(m => m.ReleaseDate)
                    .Take(8)
                    .ToListAsync();
            }

            ViewData["SimilarMovies"] = similarMovies;

            // Calculate average rating
            var averageRating = movie.Reviews.Any() ? movie.Reviews.Where(r => r.IsApproved).Average(r => r.Rating) : 0;
            ViewData["AverageRating"] = averageRating;

            // Check if user has movie in watchlist
            bool isInWatchlist = false;
            if (User.Identity.IsAuthenticated && User.IsInRole("User"))
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                isInWatchlist = await _context.WatchList
                    .AnyAsync(w => w.UserID == userId && w.MovieID == id);
            }
            ViewData["IsInWatchlist"] = isInWatchlist;

            // Check if user has already reviewed this movie
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

        [HttpGet]
        public async Task<IActionResult> LoadMore(int movieId, int skip, int take)
        {
            var reviews = await _context.Reviews
                .Where(r => r.MovieID == movieId)
                .Include(r => r.User)
                .OrderByDescending(r => r.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

            var hasMore = await _context.Reviews
                .Where(r => r.MovieID == movieId)
                .CountAsync() > skip + take;

            var reviewData = reviews.Select(r => new {
                username = r.User.Username,
                userProfileImage = r.User.ProfileImagePath,
                rating = r.Rating,
                comment = r.Comment,
                createdAt = r.CreatedAt.ToString("MMMM d, yyyy")
            });

            return Json(new { success = true, reviews = reviewData, hasMore });
        }

        // Optional: Action to manually refresh the matrix cache
        [HttpPost]
        public async Task<IActionResult> RefreshMovieMatrix()
        {
            try
            {
                var movieFeaturesList = PrepareMovieData();
                var movieVectors = ComputeTfIdfVectors(movieFeaturesList);
                await SaveMovieVectorsToFile(movieVectors);

                return Json(new { success = true, message = $"Matrix refreshed successfully. {movieVectors.Count} vectors computed." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error refreshing matrix: {ex.Message}" });
            }
        }

        // Optional: Action to get matrix info
        [HttpGet]
        public async Task<IActionResult> GetMatrixInfo()
        {
            try
            {
                if (System.IO.File.Exists(_matrixFilePath))
                {
                    var json = await System.IO.File.ReadAllTextAsync(_matrixFilePath);
                    var vectorData = JsonSerializer.Deserialize<MovieVectorData>(json);

                    return Json(new
                    {
                        exists = true,
                        lastUpdated = vectorData?.LastUpdated,
                        vectorCount = vectorData?.MovieVectors?.Count ?? 0,
                        recomputeFlag = RECOMPUTE_MATRIX
                    });
                }
                else
                {
                    return Json(new
                    {
                        exists = false,
                        recomputeFlag = RECOMPUTE_MATRIX
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    exists = false,
                    error = ex.Message,
                    recomputeFlag = RECOMPUTE_MATRIX
                });
            }
        }
    }
}