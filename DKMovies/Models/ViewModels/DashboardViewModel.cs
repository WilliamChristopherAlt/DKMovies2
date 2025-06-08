using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace DKMovies.Models.ViewModels
{
    public class DashboardViewModel
    {
        [Display(Name = "Total Users")]
        public int TotalUsers { get; set; }

        [Display(Name = "Total Employees")]
        public int TotalEmployees { get; set; }

        [Display(Name = "Total Movies")]
        public int TotalMovies { get; set; }

        [Display(Name = "Total Show Times")]
        public int TotalShowTimes { get; set; }

        [Display(Name = "Total Concessions")]
        public int TotalConcessions { get; set; }

        [Display(Name = "Total Revenue")]
        public decimal TotalRevenue { get; set; }

        [Display(Name = "Ticket Revenue")]
        public decimal TicketRevenue { get; set; }

        [Display(Name = "Concession Revenue")]
        public decimal ConcessionRevenue { get; set; }

        [Display(Name = "Current Period")]
        public string CurrentPeriod { get; set; }

        // Top Revenue Lists
        public List<TopMovieViewModel> TopMovies { get; set; } = new List<TopMovieViewModel>();
        public List<TopTheaterViewModel> TopTheaters { get; set; } = new List<TopTheaterViewModel>();
        public List<TopConcessionViewModel> TopConcessions { get; set; } = new List<TopConcessionViewModel>();

        // Helper properties for view logic
        public bool HasMovieData => TopMovies?.Any() == true;
        public bool HasTheaterData => TopTheaters?.Any() == true;
        public bool HasConcessionData => TopConcessions?.Any() == true;
    }

    public class TopMovieViewModel
    {
        public string MovieTitle { get; set; }
        public string Title => MovieTitle; // Alias for compatibility
        public decimal Revenue { get; set; }
        public decimal TotalRevenue => Revenue; // Alias for compatibility
        public string PosterImagePath { get; set; }
        public int TicketsSold { get; set; }
        public decimal PriorityScore { get; set; }
        public int ShowTimesCount { get; set; }
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public string Genre { get; set; }
        public int Duration { get; set; }
    }

    public class TopTheaterViewModel
    {
        public string TheaterName { get; set; }
        public string Location { get; set; }
        public decimal TicketRevenue { get; set; }
        public decimal ConcessionRevenue { get; set; }
        public decimal TotalRevenue { get; set; }
        public int TotalTicketsSold { get; set; }
        public int TotalAuditoriums { get; set; }
        public int TotalCapacity { get; set; }
        public double OccupancyRate { get; set; }
        public List<TheaterImageViewModel> TheaterImages { get; set; } = new List<TheaterImageViewModel>();
    }

    public class TheaterImageViewModel
    {
        public string ImageUrl { get; set; }
    }

    public class TopConcessionViewModel
    {
        public string ConcessionName { get; set; }
        public decimal Revenue { get; set; }
        public int QuantitySold { get; set; }
        public string ImagePath { get; set; }
        public decimal AveragePrice { get; set; }
        public int TheaterCount { get; set; }
        public string Category { get; set; }
    }
}