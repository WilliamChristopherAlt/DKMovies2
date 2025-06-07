using DKMovies.Models.Data;
using DKMovies.Models.Data.DatabaseModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Configuration;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace DKMovies.Controllers
{
    public class UserTheatersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private const int PageSize = 9; // Number of theaters per page
        private readonly string _googleMapsApiKey;
        private readonly IConfiguration _configuration;

        public UserTheatersController(ApplicationDbContext context, IConfiguration configuration) // Add IConfiguration parameter
        {
            _context = context;
            _configuration = configuration; // Assign it
            _googleMapsApiKey = _configuration["GoogleMaps:ApiKey"] ?? ""; // Now use _configuration
        }

        // GET: UserTheaters
        public async Task<IActionResult> Index(int page = 1, string search = "", string location = "")
        {
            try
            {
                // Get all theaters with their related data
                var theatersQuery = _context.Theaters
                    .Include(t => t.TheaterImages)
                    .Include(t => t.Auditoriums)
                    .AsQueryable();

                // Apply search filters
                if (!string.IsNullOrEmpty(search))
                {
                    theatersQuery = theatersQuery.Where(t =>
                        t.Name.Contains(search) ||
                        t.Location.Contains(search));
                }

                if (!string.IsNullOrEmpty(location))
                {
                    theatersQuery = theatersQuery.Where(t => t.Location.Contains(location));
                }

                // Calculate pagination
                var totalTheaters = await theatersQuery.CountAsync();
                var totalPages = (int)Math.Ceiling(totalTheaters / (double)PageSize);
                var theaters = await theatersQuery
                    .OrderBy(t => t.Name)
                    .Skip((page - 1) * PageSize)
                    .Take(PageSize)
                    .ToListAsync();

                // Set ViewData for pagination
                ViewData["CurrentPage"] = page;
                ViewData["TotalPages"] = totalPages;
                ViewData["Search"] = search;
                ViewData["Location"] = location;

                return View(theaters);
            }
            catch (Exception ex)
            {
                // Log the error (you should use proper logging)
                // _logger.LogError(ex, "Error loading theaters");

                // Return empty result with error message
                ViewBag.ErrorMessage = "Unable to load theaters. Please try again later.";
                return View(new List<Theater>());
            }
        }

        // Updated Details method for UserTheatersController
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var theater = await _context.Theaters
                .Include(t => t.TheaterImages.OrderBy(img => img.UploadedAt))
                .Include(t => t.Auditoriums)
                .Include(t => t.Employees)
                .FirstOrDefaultAsync(t => t.ID == id);

            if (theater == null)
            {
                return NotFound();
            }

            // Get current movies showing at this theater for today
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var currentMovies = await _context.ShowTimes
                .Include(st => st.Movie)
                    .ThenInclude(m => m.Rating)
                .Include(st => st.Movie)
                    .ThenInclude(m => m.Language)
                .Include(st => st.Movie)
                    .ThenInclude(m => m.Country)
                .Include(st => st.Auditorium)
                .Where(st => st.Auditorium.TheaterID == id &&
                             st.StartTime >= today &&
                             st.StartTime < tomorrow)
                .Select(st => st.Movie)
                .Distinct()
                .Take(8)
                .ToListAsync();

            // Get available concessions for this theater
            var theaterConcessions = await _context.TheaterConcessions
                .Include(tc => tc.Concession)
                .Where(tc => tc.TheaterID == id)
                .OrderBy(tc => tc.Concession.Name)
                .ToListAsync();

            ViewBag.CurrentMovies = currentMovies;
            ViewBag.TheaterConcessions = theaterConcessions;

            // Add Google Maps API key if you have one
            ViewData["GoogleMapsApiKey"] = _googleMapsApiKey;

            return View(theater);
        }

        // API endpoint to get nearest theaters based on user location
        [HttpPost]
        public async Task<IActionResult> GetNearestTheaters([FromBody] LocationRequest request)
        {
            if (request?.UserLat == null || request?.UserLng == null)
            {
                return Json(new List<object>());
            }

            try
            {
                var allTheaters = await _context.Theaters
                    .Include(t => t.TheaterImages)
                    .Include(t => t.Auditoriums)
                    .ToListAsync();

                var theatersWithDistance = new List<TheaterWithDistance>();

                using (var httpClient = new HttpClient())
                {
                    // Set a proper User-Agent header for Nominatim
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "DKMovies/1.0 (Theater Locator)");

                    foreach (var theater in allTheaters)
                    {
                        try
                        {
                            // Use OpenStreetMap Nominatim for geocoding
                            var nominatimUrl = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(theater.Location)}&format=json&limit=1";

                            Console.WriteLine($"\n🔍 Geocoding: {theater.Name} at '{theater.Location}'");
                            Console.WriteLine($"📍 URL: {nominatimUrl}");

                            var response = await httpClient.GetStringAsync(nominatimUrl);
                            var nominatimResults = JsonSerializer.Deserialize<NominatimResult[]>(response);

                            if (nominatimResults != null && nominatimResults.Length > 0)
                            {
                                var result = nominatimResults[0];
                                var theaterLat = double.Parse(result.lat);
                                var theaterLng = double.Parse(result.lon);

                                Console.WriteLine($"✅ Found coordinates: {theaterLat}, {theaterLng}");
                                Console.WriteLine($"📍 Display name: {result.display_name}");

                                var distance = CalculateDistance(request.UserLat.Value, request.UserLng.Value, theaterLat, theaterLng);

                                Console.WriteLine($"📏 Distance: {Math.Round(distance, 2)} km");

                                theatersWithDistance.Add(new TheaterWithDistance
                                {
                                    Theater = theater,
                                    Distance = distance
                                });
                            }
                            else
                            {
                                Console.WriteLine($"❌ No coordinates found for: {theater.Name} at '{theater.Location}'");
                            }

                            // Be respectful to Nominatim servers - add small delay
                            await Task.Delay(100);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"❌ Error geocoding {theater.Name}: {ex.Message}");
                            continue;
                        }
                    }
                }

                Console.WriteLine($"\n🎯 Found {theatersWithDistance.Count} theaters with coordinates");

                // Get 4 nearest theaters
                var nearestTheaters = theatersWithDistance
                    .OrderBy(t => t.Distance)
                    .Take(4)
                    .Select(t => new
                    {
                        id = t.Theater.ID,
                        name = t.Theater.Name,
                        location = t.Theater.Location,
                        phone = t.Theater.Phone,
                        auditoriumCount = t.Theater.Auditoriums?.Count() ?? 0,
                        distance = Math.Round(t.Distance, 1),
                        images = t.Theater.TheaterImages?.Select(img => new
                        {
                            imageUrl = img.ImageUrl,
                            uploadedAt = img.UploadedAt
                        }).Cast<object>().ToList() ?? new List<object>()
                    })
                    .ToList();

                Console.WriteLine($"🏆 Returning {nearestTheaters.Count} nearest theaters");
                foreach (var theater in nearestTheaters)
                {
                    Console.WriteLine($"  • {theater.name}: {theater.distance} km");
                }

                return Json(nearestTheaters);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Major error in GetNearestTheaters: {ex.Message}");
                return Json(new List<object>());
            }
        }

        // Calculate distance between two coordinates using Haversine formula
        private double CalculateDistance(double lat1, double lng1, double lat2, double lng2)
        {
            const double R = 6371; // Earth's radius in kilometers
            var dLat = ToRadians(lat2 - lat1);
            var dLng = ToRadians(lng2 - lng1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private double ToRadians(double degrees)
        {
            return degrees * (Math.PI / 180);
        }

        // API endpoint for search suggestions
        [HttpGet]
        public async Task<IActionResult> SearchSuggestions(string term)
        {
            if (string.IsNullOrEmpty(term) || term.Length < 2)
            {
                return Json(new List<object>());
            }

            var suggestions = await _context.Theaters
                .Where(t => t.Name.Contains(term) || t.Location.Contains(term))
                .Select(t => new {
                    name = t.Name,
                    location = t.Location,
                    id = t.ID
                })
                .Take(5)
                .ToListAsync();

            return Json(suggestions);
        }

        // API endpoint to get theaters by location
        [HttpGet]
        public async Task<IActionResult> GetTheatersByLocation(string location)
        {
            if (string.IsNullOrEmpty(location))
            {
                return Json(new List<object>());
            }

            var theaters = await _context.Theaters
                .Include(t => t.Auditoriums)
                .Where(t => t.Location.Contains(location))
                .Select(t => new {
                    id = t.ID,
                    name = t.Name,
                    location = t.Location,
                    phone = t.Phone,
                    auditoriumCount = t.Auditoriums.Count()
                })
                .ToListAsync();

            return Json(theaters);
        }

        // Helper method to get unique locations for filter dropdown
        [HttpGet]
        public async Task<IActionResult> GetLocations()
        {
            var locations = await _context.Theaters
                .Select(t => t.Location)
                .Distinct()
                .OrderBy(l => l)
                .ToListAsync();

            return Json(locations);
        }

        [HttpPost]
        public async Task<IActionResult> GeocodeLocation([FromBody] GeocodeRequest request)
        {
            if (string.IsNullOrEmpty(request?.Address))
            {
                return Json(new { lat = (double?)null, lng = (double?)null });
            }

            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", "DKMovies/1.0");

                var url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(request.Address)}&format=json&limit=1";
                var response = await httpClient.GetStringAsync(url);
                var results = JsonSerializer.Deserialize<NominatimResult[]>(response);

                if (results?.Length > 0)
                {
                    return Json(new
                    {
                        lat = double.Parse(results[0].lat),
                        lng = double.Parse(results[0].lon)
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Geocoding error: {ex.Message}");
            }

            return Json(new { lat = (double?)null, lng = (double?)null });
        }

        public class GeocodeRequest
        {
            public string Address { get; set; }
        }
    }

    // Helper classes for location-based functionality
    public class LocationRequest
    {
        public double? UserLat { get; set; }
        public double? UserLng { get; set; }
    }

    public class TheaterWithDistance
    {
        public Theater Theater { get; set; }
        public double Distance { get; set; }
    }

    // OpenStreetMap Nominatim response model
    public class NominatimResult
    {
        public string lat { get; set; }
        public string lon { get; set; }
        public string display_name { get; set; }
        public long place_id { get; set; }
        public string osm_type { get; set; }
        public long osm_id { get; set; }
    }
}