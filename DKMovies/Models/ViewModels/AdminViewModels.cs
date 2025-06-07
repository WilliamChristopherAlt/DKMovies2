using DKMovies.Models.Data.DatabaseModels;

namespace DKMovies.Models.ViewModels
{
    public class MovieIndexViewModel
    {
        public IEnumerable<Movie> Movies { get; set; } = new List<Movie>();
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TotalMovies { get; set; }
        public int PageSize { get; set; }
        public string SearchTerm { get; set; } = "";
        public string FilterType { get; set; } = "all";
        public bool HasPreviousPage { get; set; }
        public bool HasNextPage { get; set; }

        public int StartItem => TotalMovies == 0 ? 0 : (CurrentPage - 1) * PageSize + 1;
        public int EndItem => Math.Min(CurrentPage * PageSize, TotalMovies);
    }

    // ViewModel for ShowTime Index
    public class ShowTimeIndexViewModel
    {
        public IEnumerable<ShowTime> ShowTimes { get; set; } = new List<ShowTime>();
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TotalShowtimes { get; set; }
        public int PageSize { get; set; }
        public string SearchTerm { get; set; } = string.Empty;
        public string FilterType { get; set; } = "all";
        public bool HasPreviousPage { get; set; }
        public bool HasNextPage { get; set; }
        public dynamic Statistics { get; set; }
    }

    public class UserIndexViewModel
    {
        public IEnumerable<User> Users { get; set; } = new List<User>();
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TotalUsers { get; set; }
        public int PageSize { get; set; }
        public string SearchTerm { get; set; } = "";
        public string FilterType { get; set; } = "all";
        public bool HasPreviousPage { get; set; }
        public bool HasNextPage { get; set; }

        public int StartItem => (CurrentPage - 1) * PageSize + 1;
        public int EndItem => Math.Min(CurrentPage * PageSize, TotalUsers);
    }

    public class EmployeeIndexViewModel
    {
        public IEnumerable<Employee> Employees { get; set; } = new List<Employee>();
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int TotalEmployees { get; set; } = 0;
        public int PageSize { get; set; } = 20;
        public string SearchTerm { get; set; } = string.Empty;
        public string FilterType { get; set; } = "all";
        public bool HasPreviousPage { get; set; } = false;
        public bool HasNextPage { get; set; } = false;

        // Calculated properties for display
        public int StartItem => TotalEmployees == 0 ? 0 : ((CurrentPage - 1) * PageSize) + 1;
        public int EndItem => Math.Min(CurrentPage * PageSize, TotalEmployees);
    }

    public class ConcessionIndexViewModel
    {
        public IEnumerable<Concession> Concessions { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TotalConcessions { get; set; }
        public int PageSize { get; set; }
        public string SearchTerm { get; set; }
        public bool HasPreviousPage { get; set; }
        public bool HasNextPage { get; set; }

        public int StartItem => (CurrentPage - 1) * PageSize + 1;
        public int EndItem => Math.Min(CurrentPage * PageSize, TotalConcessions);
    }

    public class ActorIndexViewModel
    {
        public IEnumerable<Actor> Actors { get; set; } = new List<Actor>();
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TotalActors { get; set; }
        public int PageSize { get; set; }
        public string SearchTerm { get; set; } = "";
        public string FilterType { get; set; } = "all";
        public bool HasPreviousPage { get; set; }
        public bool HasNextPage { get; set; }

        // Calculated properties for pagination display
        public int StartItem => (CurrentPage - 1) * PageSize + 1;
        public int EndItem => Math.Min(CurrentPage * PageSize, TotalActors);
    }

    public class DirectorIndexViewModel
    {
        public IEnumerable<Director> Directors { get; set; } = new List<Director>();
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TotalDirectors { get; set; }
        public int PageSize { get; set; }
        public string SearchTerm { get; set; } = string.Empty;
        public string FilterType { get; set; } = "all";
        public bool HasPreviousPage { get; set; }
        public bool HasNextPage { get; set; }
        public int StartItem => (CurrentPage - 1) * PageSize + 1;
        public int EndItem => Math.Min(CurrentPage * PageSize, TotalDirectors);
    }
}