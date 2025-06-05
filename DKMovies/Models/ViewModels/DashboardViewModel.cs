using System.Collections.Generic;

namespace DKMovies.Models.ViewModels
{
    public class DashboardViewModel
    {
        public int TotalUsers { get; set; }
        public int TotalEmployees { get; set; }
        public int TotalMovies { get; set; }
        public int TotalShowTimes { get; set; }
        public int TotalConcessions { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TicketRevenue { get; set; }
        public decimal ConcessionRevenue { get; set; }


        // ✅ THÊM: Property cho Movie Analytics
        public List<MovieScoreViewModel> TopMovies { get; set; } = new List<MovieScoreViewModel>();

        // ✅ THÊM: Additional dashboard metrics
        public int TodayTickets { get; set; }
        public decimal ThisMonthRevenue { get; set; }
        public int ActiveShowtimes { get; set; }
        public double AverageRating { get; set; }

        // ✅ THÊM: Helper properties
        public bool HasMovieData => TopMovies != null && TopMovies.Any();
        public string FormattedTotalRevenue => TotalRevenue.ToString("N0") + " ₫";
        public string FormattedMonthRevenue => ThisMonthRevenue.ToString("N0") + " ₫";
    }
}