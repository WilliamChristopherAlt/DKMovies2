using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using DKMovies.Models;

namespace DKMovies.Controllers
{
    public class ShowTimesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ShowTimesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: ShowTimes
        public async Task<IActionResult> Index(string title, int? subtitleLang, bool? is3D)
        {
            var query = _context.ShowTimes
                .Include(s => s.Movie)
                .Include(s => s.Auditorium)
                    .ThenInclude(a => a.Theater)
                .Include(s => s.SubtitleLanguage)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(title))
                query = query.Where(s => s.Movie.Title.Contains(title));

            if (subtitleLang.HasValue)
                query = query.Where(s => s.SubtitleLanguageID == subtitleLang.Value);

            if (is3D.HasValue)
                query = query.Where(s => s.Is3D == is3D.Value);

            ViewBag.SubtitleLanguages = await _context.Languages.ToListAsync();
            return View(await query.ToListAsync());
        }

        // GET: ShowTimes/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var showTime = await _context.ShowTimes
                .Include(s => s.Auditorium)
                .Include(s => s.Movie)
                .Include(s => s.SubtitleLanguage)
                .FirstOrDefaultAsync(m => m.ID == id);
            if (showTime == null)
            {
                return NotFound();
            }

            return View(showTime);
        }

        [HttpGet]
        public async Task<JsonResult> GetAuditoriumsByTheater(int theaterId)
        {
            var auditoriums = await _context.Auditoriums
                .Where(a => a.TheaterID == theaterId)
                .OrderBy(a => a.Name)
                .Select(a => new { id = a.ID, name = a.Name })
                .ToListAsync();

            return Json(auditoriums);
        }

        // GET: ShowTimes/Create
        public async Task<IActionResult> Create(int? movieId)
        {
            var showtime = new ShowTime();

            var movies = await _context.Movies
                .OrderBy(m => m.Title)
                .Select(m => new { m.ID, m.Title, m.DurationMinutes })
                .ToListAsync();
            ViewBag.Movies = movies;

            if (movieId.HasValue)
            {
                var movie = movies.FirstOrDefault(m => m.ID == movieId.Value);
                if (movie != null)
                {
                    showtime.MovieID = movie.ID;
                    ViewBag.MovieID = movie.ID;
                    ViewBag.MovieTitle = movie.Title;
                    ViewBag.MovieDuration = movie.DurationMinutes;
                }
            }

            ViewBag.Theaters = await _context.Theaters.OrderBy(t => t.Name).ToListAsync();
            ViewData["LanguageID"] = new SelectList(_context.Languages, "ID", "Name");

            return View(showtime);
        }

        // POST: ShowTimes/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ID,MovieID,AuditoriumID,StartTime,SubtitleLanguageID,Is3D")] ShowTime showTime, int? TheaterID)
        {
            // Ensure MovieID is present
            if (showTime.MovieID == 0) // Or check if it's null, depending on your model
            {
                ModelState.AddModelError("MovieID", "The MovieID field is required.");
            }

            var movie = await _context.Movies.FirstOrDefaultAsync(m => m.ID == showTime.MovieID);
            if (movie == null && showTime.MovieID != 0) // Add check if movieID was actually provided
            {
                ModelState.AddModelError("MovieID", "Selected movie does not exist.");
            }

            // Ensure AuditoriumID is present
            if (showTime.AuditoriumID == 0) // Or check if it's null, depending on your model
            {
                ModelState.AddModelError("AuditoriumID", "The AuditoriumID field is required.");
            }
            else
            {
                var auditorium = await _context.Auditoriums.FirstOrDefaultAsync(a => a.ID == showTime.AuditoriumID);
                if (auditorium == null)
                {
                    ModelState.AddModelError("AuditoriumID", "Selected auditorium does not exist.");
                }
            }


            if (movie != null) // Only proceed with duration and conflict check if movie is valid
            {
                showTime.DurationMinutes = movie.DurationMinutes;

                // Conflict check logic
                var newShowStart = showTime.StartTime;
                var newShowEnd = showTime.StartTime.AddMinutes(movie.DurationMinutes);

                // Your existing conflict check logic
                var conflictingShowtimeExists = await _context.ShowTimes
                    .Where(s => s.AuditoriumID == showTime.AuditoriumID)
                    .AnyAsync(s =>
                        newShowStart < s.StartTime.AddMinutes(s.DurationMinutes + 30) &&
                        newShowEnd.AddMinutes(30) > s.StartTime
                    );

                if (conflictingShowtimeExists)
                {
                    ModelState.AddModelError("", "This showtime conflicts with another showtime in the same auditorium.");
                }
            }
            foreach (var kvp in ModelState)
            {
                foreach (var error in kvp.Value.Errors)
                {
                    Console.WriteLine($"Key: {kvp.Key}, Error: {error.ErrorMessage}");
                }
            }

            if (ModelState.IsValid)
            {
                _context.Add(showTime);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            // Repopulate ViewBags/Data for view if ModelState is not valid
            ViewBag.Theaters = await _context.Theaters.OrderBy(t => t.Name).ToListAsync();
            ViewData["LanguageID"] = new SelectList(_context.Languages, "ID", "Name", showTime.SubtitleLanguageID);

            if (TheaterID.HasValue)
            {
                var auditoriums = await _context.Auditoriums
                    .Where(a => a.TheaterID == TheaterID.Value)
                    .OrderBy(a => a.Name)
                    .ToListAsync();
                ViewData["AuditoriumID"] = new SelectList(auditoriums, "ID", "Name", showTime.AuditoriumID);
                ViewBag.SelectedTheaterID = TheaterID.Value;
            }
            else
            {
                ViewData["AuditoriumID"] = new SelectList(Enumerable.Empty<Auditorium>(), "ID", "Name");
            }

            if (movie != null)
            {
                ViewBag.MovieTitle = movie.Title;
                ViewBag.MovieDuration = movie.DurationMinutes;
            }
            // Set MovieID for the hidden input if it's not already on the model from binding
            ViewBag.MovieID = showTime.MovieID;


            return View(showTime);
        }

        // GET: ShowTimes/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var showTime = await _context.ShowTimes
                .Include(s => s.Auditorium)
                .ThenInclude(a => a.Theater)
                .FirstOrDefaultAsync(s => s.ID == id);

            if (showTime == null) return NotFound();

            var movie = await _context.Movies.FindAsync(showTime.MovieID);
            ViewBag.MovieID = new SelectList(_context.Movies, "ID", "Title", showTime.MovieID);
            ViewBag.MovieTitle = movie?.Title;
            ViewBag.MovieDuration = movie?.DurationMinutes ?? 0;

            var selectedTheaterId = showTime.Auditorium?.TheaterID ?? 0;
            var theaters = await _context.Theaters.OrderBy(t => t.Name).ToListAsync();
            ViewBag.Theaters = new SelectList(theaters, "ID", "Name", selectedTheaterId);


            var auditoriums = await _context.Auditoriums
                .Where(a => a.TheaterID == selectedTheaterId)
                .OrderBy(a => a.Name)
                .ToListAsync();
            ViewData["AuditoriumID"] = new SelectList(auditoriums, "ID", "Name", showTime.AuditoriumID);
            ViewData["LanguageID"] = new SelectList(_context.Languages, "ID", "Name", showTime.SubtitleLanguageID);

            return View(showTime);
        }

        // POST: ShowTimes/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ID,MovieID,AuditoriumID,StartTime,DurationMinutes,SubtitleLanguageID,Is3D")] ShowTime showTime, int? TheaterID)
        {
            if (id != showTime.ID) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(showTime);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.ShowTimes.Any(e => e.ID == showTime.ID))
                        return NotFound();
                    throw;
                }
            }

            var movie = await _context.Movies.FindAsync(showTime.MovieID);
            ViewBag.MovieID = new SelectList(_context.Movies, "ID", "Title", showTime.MovieID);
            ViewBag.MovieTitle = movie?.Title;
            ViewBag.MovieDuration = movie?.DurationMinutes ?? 0;

            ViewBag.SelectedTheaterID = TheaterID ?? 0;
            ViewBag.Theaters = await _context.Theaters.OrderBy(t => t.Name).ToListAsync();

            var auditoriums = await _context.Auditoriums
                .Where(a => a.TheaterID == TheaterID)
                .OrderBy(a => a.Name)
                .ToListAsync();
            ViewData["AuditoriumID"] = new SelectList(auditoriums, "ID", "Name", showTime.AuditoriumID);
            ViewData["LanguageID"] = new SelectList(_context.Languages, "ID", "Name", showTime.SubtitleLanguageID);

            return View(showTime);
        }


        // GET: ShowTimes/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var showTime = await _context.ShowTimes
                .Include(s => s.Auditorium)
                .Include(s => s.Movie)
                .Include(s => s.SubtitleLanguage)
                .FirstOrDefaultAsync(m => m.ID == id);
            if (showTime == null)
            {
                return NotFound();
            }

            return View(showTime);
        }

        // POST: ShowTimes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var showTime = await _context.ShowTimes.FindAsync(id);
            if (showTime != null)
            {
                _context.ShowTimes.Remove(showTime);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ShowTimeExists(int id)
        {
            return _context.ShowTimes.Any(e => e.ID == id);
        }
    }
}