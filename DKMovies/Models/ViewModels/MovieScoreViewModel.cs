// Models/ViewModels/MovieScoreViewModel.cs
namespace DKMovies.Models.ViewModels
{
    public class MovieScoreViewModel
    {
        public int MovieID { get; set; }
        public string Title { get; set; } = string.Empty;
        public int TicketsSold { get; set; }
        public decimal TotalRevenue { get; set; }
        public double AvgRating { get; set; }
        public double PriorityScore { get; set; }
    }
}