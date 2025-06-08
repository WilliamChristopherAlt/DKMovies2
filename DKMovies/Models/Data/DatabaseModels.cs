using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Stripe;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace DKMovies.Models.Data.DatabaseModels
{
    // 1. COUNTRIES
    public class Country
    {
        [Key]
        [Display(Name = "Country ID")]
        public int ID { get; set; }

        [Required, MaxLength(100)]
        [Display(Name = "Country Name")]
        public string Name { get; set; }

        [MaxLength(255)]
        [Display(Name = "Description")]
        public string Description { get; set; }

        [NotMapped]
        public ICollection<Movie> Movies { get; set; }
    }

    // 2. GENRES
    public class Genre
    {
        [Key]
        [Display(Name = "Genre ID")]
        public int ID { get; set; }

        [Required, MaxLength(100)]
        [Display(Name = "Genre Name")]
        public string Name { get; set; }

        [MaxLength(255)]
        [Display(Name = "Description")]
        public string Description { get; set; }

        public ICollection<MovieGenre> MovieGenres { get; set; }
    }

    // 3. RATINGS
    public class Rating
    {
        [Key]
        [Display(Name = "Rating ID")]
        public int ID { get; set; }

        [Required, MaxLength(10)]
        [Display(Name = "Rating Value")]
        public string Value { get; set; }

        [MaxLength(255)]
        [Display(Name = "Description")]
        public string Description { get; set; }

        public ICollection<Movie> Movies { get; set; }
    }

    // 4. LANGUAGES
    public class Language
    {
        [Key]
        [Display(Name = "Language ID")]
        public int ID { get; set; }

        [Required, MaxLength(100)]
        [Display(Name = "Language Name")]
        public string Name { get; set; }

        [Display(Name = "Description")]
        public string Description { get; set; }

        public ICollection<Movie> Movies { get; set; }
        public ICollection<ShowTime> ShowTimes { get; set; }
    }

    // 5. DIRECTORS
    public class Director
    {
        [Key]
        [Display(Name = "Director ID")]
        public int ID { get; set; }

        [Required, MaxLength(255)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; }

        [Display(Name = "Date of Birth")]
        public DateTime? DateOfBirth { get; set; }

        [Display(Name = "Biography")]
        public string Biography { get; set; }

        [MaxLength(255)]
        [Display(Name = "Place of Birth")]
        public string? PlaceOfBirth { get; set; }

        [MaxLength(500)]
        [Display(Name = "Profile Image Path")]
        public string? ProfileImagePath { get; set; }

        public ICollection<Movie> Movies { get; set; }
    }

    // 12. ACTORS
    public class Actor
    {
        [Key]
        [Display(Name = "Actor ID")]
        public int ID { get; set; }

        [Required, MaxLength(255)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; }

        [Display(Name = "Biography")]
        public string? Biography { get; set; }

        [Display(Name = "Date of Birth")]
        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        [MaxLength(255)]
        [Display(Name = "Place of Birth")]
        public string? PlaceOfBirth { get; set; }

        [MaxLength(255)]
        [Display(Name = "Profile Image Path")]
        public string? ProfileImagePath { get; set; }

        public ICollection<MovieActor> MovieActors { get; set; }
    }

    // 6. USERS
    public class User
    {
        [Key]
        [Display(Name = "User ID")]
        public int ID { get; set; }

        [Required, MaxLength(100)]
        [Display(Name = "Username")]
        public string Username { get; set; }

        [Required, MaxLength(255)]
        [Display(Name = "Email")]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [Display(Name = "Password Hash")]
        public string PasswordHash { get; set; }

        [MaxLength(255)]
        [Display(Name = "Full Name")]
        public string? FullName { get; set; }

        [MaxLength(20)]
        [Display(Name = "Phone Number")]
        [Phone]
        public string? Phone { get; set; }

        [Display(Name = "Birth Date")]
        public DateTime? BirthDate { get; set; }

        [MaxLength(10)]
        [Display(Name = "Gender")]
        public string? Gender { get; set; }

        [MaxLength(500)]
        [Display(Name = "Profile Image Path")]
        public string? ProfileImagePath { get; set; }

        [Display(Name = "Created At")]
        public DateTime CreatedAt { get; set; }

        [Display(Name = "Email Confirmed")]
        public bool EmailConfirmed { get; set; } = false;

        [MaxLength(100)]
        [Display(Name = "Confirmation Code")]
        public string? ConfirmationCode { get; set; }

        [Display(Name = "Two-Factor Enabled")]
        public bool TwoFactorEnabled { get; set; } = false;

        [MaxLength(100)]
        [Display(Name = "2FA Code")]
        public string? TwoFactorCode { get; set; }

        [Display(Name = "2FA Expiry Time")]
        public DateTime? TwoFactorExpiry { get; set; }

        public ICollection<Ticket> Tickets { get; set; }
        public ICollection<Review> Reviews { get; set; }
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
        public ICollection<LoginAttempt> LoginAttempts { get; set; }
        public ICollection<ReviewReaction> ReviewReactions { get; set; }
    }

    // 7. THEATERS
    public class Theater
    {
        [Key]
        [Display(Name = "Theater ID")]
        public int ID { get; set; }

        [Required, MaxLength(255)]
        [Display(Name = "Theater Name")]
        public string Name { get; set; }

        [Required, MaxLength(255)]
        [Display(Name = "Location")]
        public string Location { get; set; }

        [MaxLength(20)]
        [Phone]
        [Display(Name = "Phone Number")]
        public string Phone { get; set; }

        public ICollection<Auditorium> Auditoriums { get; set; }
        public ICollection<Employee> Employees { get; set; }
        public ICollection<TheaterImage> TheaterImages { get; set; }
        public ICollection<TheaterConcession> TheaterConcessions { get; set; }
    }

    public class TheaterImage
    {
        [Key]
        public int ID { get; set; }

        [Required]
        public int TheaterID { get; set; }

        [Required, MaxLength(500)]
        [Display(Name = "Image URL")]
        public string ImageUrl { get; set; }

        [Display(Name = "Uploaded At")]
        public DateTime UploadedAt { get; set; } = DateTime.Now;

        // Navigation Property
        public Theater Theater { get; set; }
    }

    // 8. AUDITORIUMS
    public class Auditorium
    {
        [Key]
        [Display(Name = "Auditorium ID")]
        public int ID { get; set; }

        [Display(Name = "Theater")]
        public int TheaterID { get; set; }

        [ForeignKey("TheaterID")]
        [Display(Name = "Theater")]
        public Theater Theater { get; set; }

        [Required, MaxLength(50)]
        [Display(Name = "Auditorium Name")]
        public string Name { get; set; }

        [Display(Name = "Capacity")]
        public int Capacity { get; set; }

        public ICollection<Seat> Seats { get; set; }
        public ICollection<ShowTime> ShowTimes { get; set; }
    }

    // 9. SEATS
    public class Seat
    {
        [Key]
        [Display(Name = "Seat ID")]
        public int ID { get; set; }

        [Display(Name = "Auditorium")]
        public int AuditoriumID { get; set; }

        [ForeignKey("AuditoriumID")]
        [Display(Name = "Auditorium")]
        public Auditorium Auditorium { get; set; }

        [Required, MaxLength(1)]
        [Display(Name = "Row Label")]
        public string RowLabel { get; set; }

        [Display(Name = "Seat Number")]
        public int SeatNumber { get; set; }

        public ICollection<TicketSeat> TicketSeats { get; set; }
    }

    // 10. MOVIES
    public class Movie
    {
        [Key]
        [Display(Name = "Movie ID")]
        public int ID { get; set; }

        [Required, MaxLength(255)]
        [Display(Name = "Movie Title")]
        public string Title { get; set; }

        [Display(Name = "Description")]
        public string Description { get; set; }

        [Display(Name = "Duration (Minutes)")]
        public int DurationMinutes { get; set; }

        [ForeignKey("RatingID")]
        [Display(Name = "Rating")]
        public int RatingID { get; set; }

        public Rating Rating { get; set; }

        [Display(Name = "Release Date")]
        public DateTime? ReleaseDate { get; set; }

        [ForeignKey("LanguageID")]
        [Display(Name = "Language")]
        public int LanguageID { get; set; }

        public Language Language { get; set; }

        [ForeignKey("CountryID")]
        [Display(Name = "Country")]
        public int? CountryID { get; set; }

        public Country Country { get; set; }

        [ForeignKey("DirectorID")]
        [Display(Name = "Director")]
        public int? DirectorID { get; set; }

        public Director Director { get; set; }

        [Display(Name = "Poster Image Path")]
        public string? PosterImagePath { get; set; }

        [Display(Name = "Wallpaper Image Path")]
        public string? WallpaperImagePath { get; set; }

        public string? TrailerUrl { get; set; }

        public ICollection<MovieGenre> MovieGenres { get; set; }
        public ICollection<ShowTime> ShowTimes { get; set; }
        public ICollection<Review> Reviews { get; set; }
        public ICollection<MovieActor> MovieActors { get; set; }
        [NotMapped]
        public IEnumerable<Actor> Actors => MovieActors?.Select(ma => ma.Actor);
    }

    public class MovieActor
    {
        [Key]
        [Display(Name = "Movie-Actor ID")]
        public int ID { get; set; }

        [Display(Name = "Movie ID")]
        public int MovieID { get; set; }

        [ForeignKey("MovieID")]
        public Movie Movie { get; set; }

        [Display(Name = "Actor ID")]
        public int ActorID { get; set; }

        [ForeignKey("ActorID")]
        public Actor Actor { get; set; }

        [MaxLength(255)]
        [Display(Name = "Role")]
        public string? Role { get; set; }
    }


    // 11. MOVIEGENRE
    public class MovieGenre
    {
        [Key]
        [Display(Name = "Movie-Genre ID")]
        public int ID { get; set; }

        [Display(Name = "Movie ID")]
        public int MovieID { get; set; }

        [Display(Name = "Genre ID")]
        public int GenreID { get; set; }

        [ForeignKey("MovieID")]
        public Movie Movie { get; set; }

        [ForeignKey("GenreID")]
        public Genre Genre { get; set; }
    }

    public class WatchListSingular
    {
        [Key]
        [Display(Name = "Watchlist ID")]
        public int ID { get; set; }

        [Display(Name = "User")]
        public int UserID { get; set; }

        [ForeignKey("UserID")]
        public User User { get; set; }

        [Display(Name = "Movie ID")]
        public int MovieID { get; set; }

        [ForeignKey("MovieID")]
        public Movie Movie { get; set; }

        [Display(Name = "Added At")]
        public DateTime AddedAt { get; set; } = DateTime.Now;
    }

    // 12. SHOWTIMES
    public class ShowTime
    {
        [Key]
        [Display(Name = "Showtime ID")]
        public int ID { get; set; }

        [Display(Name = "Movie ID")]
        public int MovieID { get; set; } = 0;
        [ValidateNever]
        [ForeignKey("MovieID")]
        public Movie Movie { get; set; }

        [Display(Name = "Auditorium ID")]
        public int AuditoriumID { get; set; } = 0;
        [ValidateNever]
        [ForeignKey("AuditoriumID")]
        public Auditorium Auditorium { get; set; }

        [Display(Name = "Start Time")]
        public DateTime StartTime { get; set; }

        [Display(Name = "Duration (minutes)")]
        public int DurationMinutes { get; set; }

        [Display(Name = "Subtitle Language ID")]
        public int? SubtitleLanguageID { get; set; }
        [ValidateNever]
        [ForeignKey("SubtitleLanguageID")]
        public Language? SubtitleLanguage { get; set; }

        [Display(Name = "3D")]
        public bool Is3D { get; set; }

        [Display(Name = "Price")]
        public decimal Price { get; set; }
        [ValidateNever]
        public ICollection<Ticket> Tickets { get; set; }
    }

    public enum TicketStatus
    {
        PENDING,
        PAID,
        CONFIRMED,
        CANCELLED
    }

    // 13. TICKETS
    public class Ticket
    {
        [Key]
        [Display(Name = "Ticket ID")]
        public int ID { get; set; }

        [Display(Name = "User ID")]
        public int UserID { get; set; }

        [ForeignKey("UserID")]
        public User User { get; set; }

        [Display(Name = "Showtime ID")]
        public int ShowTimeID { get; set; }

        [ForeignKey("ShowTimeID")]
        public ShowTime ShowTime { get; set; }

        [Display(Name = "Purchase Time")]
        public DateTime PurchaseTime { get; set; }

        [Required]
        [Display(Name = "Status")]
        public TicketStatus Status { get; set; } = TicketStatus.PENDING;

        [NotMapped]
        [Display(Name = "Total Price")]
        public decimal TotalPrice
        {
            get
            {
                decimal seatTotal = (TicketSeats?.Count ?? 0) * (ShowTime?.Price ?? 0);

                decimal concessionTotal = OrderItems?.Sum(item =>
                    item.PriceAtPurchase * item.Quantity) ?? 0;

                return seatTotal + concessionTotal;
            }
        }

        public ICollection<TicketPayment> TicketPayments { get; set; }
        public ICollection<TicketSeat> TicketSeats { get; set; }
        public ICollection<OrderItem> OrderItems { get; set; }
    }

    public class TicketSeat
    {
        [Key]
        [Display(Name = "Ticket Seat ID")]
        public int ID { get; set; }

        [Display(Name = "Ticket ID")]
        public int TicketID { get; set; }

        [ForeignKey("TicketID")]
        public Ticket Ticket { get; set; }

        [Display(Name = "Seat ID")]
        public int SeatID { get; set; }

        [ForeignKey("SeatID")]
        public Seat Seat { get; set; }
    }

    public enum PaymentStatus
    {
        PENDING,
        COMPLETED,
        FAILED
    }

    public enum PaymentMethod
    {
        CARD,
        AFTERPAY_CLEARPAY,
        ALIPAY,
        GRABPAY,
        IDEAL,
        KONBINI,
        SEPA_DEBIT,
        SOFORT
    }


    // 15. TICKET PAYMENTS
    public class TicketPayment
    {
        [Key]
        [Display(Name = "Payment ID")]
        public int ID { get; set; }

        [Display(Name = "Ticket ID")]
        public int TicketID { get; set; }

        [ForeignKey("TicketID")]
        public Ticket Ticket { get; set; }

        [Required]
        [Display(Name = "Payment Method")]
        public PaymentMethod Method { get; set; }

        [Required]
        [Display(Name = "Payment Status")]
        public PaymentStatus PaymentStatus { get; set; }

        [Display(Name = "Paid Amount")]
        public decimal? PaidAmount { get; set; }

        [Display(Name = "Paid At")]
        public DateTime? PaidAt { get; set; }
    }


    // 16. EMPLOYEE ROLES
    public class EmployeeRole
    {
        [Key]
        [Display(Name = "Role ID")]
        public int ID { get; set; }

        [Required, MaxLength(100)]
        [Display(Name = "Role Name")]
        public string Name { get; set; }

        [MaxLength(255)]
        [Display(Name = "Description")]
        public string Description { get; set; }

        public ICollection<Employee> Employees { get; set; }
    }

    // 17. EMPLOYEES
    public class Employee
    {
        [Key]
        [Display(Name = "Employee ID")]
        public int ID { get; set; }

        [Display(Name = "Theater ID")]
        public int TheaterID { get; set; }

        [ForeignKey("TheaterID")]
        public Theater Theater { get; set; }

        [Display(Name = "Role ID")]
        public int RoleID { get; set; }

        [ForeignKey("RoleID")]
        public EmployeeRole Role { get; set; }

        [Required, MaxLength(255)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; }

        [Required, MaxLength(255)]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [MaxLength(20)]
        [Display(Name = "Phone Number")]
        public string Phone { get; set; }

        [MaxLength(10)]
        [Display(Name = "Gender")]
        public string Gender { get; set; }

        [Display(Name = "Date of Birth")]
        public DateTime? DateOfBirth { get; set; }

        [MaxLength(50)]
        [Display(Name = "Citizen ID")]
        public string CitizenID { get; set; }

        [MaxLength(255)]
        [Display(Name = "Address")]
        public string Address { get; set; }

        [Display(Name = "Hire Date")]
        public DateTime HireDate { get; set; }

        [Display(Name = "Salary")]
        public decimal? Salary { get; set; }

        [MaxLength(500)]
        [Display(Name = "Profile Image Path")]
        public string? ProfileImagePath { get; set; }

        public ICollection<Admin> Admins { get; set; }
    }

    // 19. CONCESSIONS
    public class Concession
    {
        [Key]
        [Display(Name = "Concession ID")]
        public int ID { get; set; }

        [Required, MaxLength(100)]
        [Display(Name = "Concession Name")]
        public string Name { get; set; }

        [MaxLength(255)]
        public string Description { get; set; }

        [Display(Name = "Image Path")]
        public string? ImagePath { get; set; }

        // Navigation
        public ICollection<TheaterConcession> TheaterConcessions { get; set; }
    }

    // 20. THEATER CONCESSIONS
    public class TheaterConcession
    {
        [Key]
        [Display(Name = "Theater Concession ID")]
        public int ID { get; set; }

        [Display(Name = "Theater ID")]
        public int TheaterID { get; set; }

        [ForeignKey("TheaterID")]
        public Theater Theater { get; set; }

        [Display(Name = "Concession ID")]
        public int ConcessionID { get; set; }

        [ForeignKey("ConcessionID")]
        public Concession Concession { get; set; }

        [Display(Name = "Price")]
        public decimal Price { get; set; }

        [Display(Name = "Stock Left")]
        public int StockLeft { get; set; }

        [Display(Name = "Is Available")]
        public bool IsAvailable { get; set; }

        // Navigation
        public ICollection<OrderItem> OrderItems { get; set; }
    }


    // 21. ORDER ITEMS
    public class OrderItem
    {
        [Key]
        [Display(Name = "Order Item ID")]
        public int ID { get; set; }

        [Display(Name = "Ticket ID")]
        public int TicketID { get; set; }

        [ForeignKey("TicketID")]
        public Ticket Ticket { get; set; }

        [Display(Name = "Theater Concession ID")]
        public int TheaterConcessionID { get; set; }

        [ForeignKey("TheaterConcessionID")]
        public TheaterConcession TheaterConcession { get; set; }

        [Display(Name = "Quantity")]
        public int Quantity { get; set; }

        [Display(Name = "Price At Purchase")]
        public decimal PriceAtPurchase { get; set; }
    }


    // 23. REVIEWS
    public class Review
    {
        [Key]
        [Display(Name = "Review ID")]
        public int ID { get; set; }

        [Display(Name = "Movie ID")]
        [Required]
        public int MovieID { get; set; }

        [NotMapped]
        [ForeignKey("MovieID")]
        public Movie Movie { get; set; }

        [Display(Name = "User ID")]
        [Required]
        public int UserID { get; set; }

        [NotMapped]
        [ForeignKey("UserID")]
        public User User { get; set; }

        [Display(Name = "Rating")]
        public int Rating { get; set; }

        [Display(Name = "Comment")]
        public string Comment { get; set; }

        [Display(Name = "Created At")]
        public DateTime CreatedAt { get; set; }

        [Display(Name = "Is Approved")]
        public bool IsApproved { get; set; }

        // Navigation
        public ICollection<ReviewReaction> ReviewReactions { get; set; }
    }

    public class ReviewReaction
    {
        [Key]
        [Display(Name = "Reaction ID")]
        public int ID { get; set; }

        [Required]
        [Display(Name = "Review ID")]
        public int ReviewID { get; set; }

        [ForeignKey("ReviewID")]
        public Review Review { get; set; }

        [Required]
        [Display(Name = "User ID")]
        public int UserID { get; set; }

        [ForeignKey("UserID")]
        public User User { get; set; }

        [Required]
        [Display(Name = "Is Like")]
        public bool IsLike { get; set; } // true for like, false for dislike

        [Display(Name = "Created At")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    // 24. ADMINS
    public class Admin
    {
        [Key]
        [Display(Name = "Admin ID")]
        public int ID { get; set; }

        [Display(Name = "Employee ID")]
        public int EmployeeID { get; set; }

        [ForeignKey("EmployeeID")]
        public Employee Employee { get; set; }

        [Required, MaxLength(100)]
        [Display(Name = "Username")]
        public string Username { get; set; }

        [Required]
        [Display(Name = "Password Hash")]
        public string PasswordHash { get; set; }

        [Display(Name = "Created At")]
        public DateTime CreatedAt { get; set; }

        // Navigation properties with explicit inverse properties
        [InverseProperty("Admin")]
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();

        public ICollection<LoginAttempt> LoginAttempts { get; set; } = new List<LoginAttempt>();
    }

    public class Message
    {
        [Key]
        public int ID { get; set; }

        [Required]
        public int UserID { get; set; }

        [Required]
        public bool IsFromUser { get; set; } // true = user, false = system/admin

        [Required]
        public byte[] MessageText { get; set; } // Binary storage (e.g., encrypted); use string if not

        [Required]
        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        public DateTime? ReadAt { get; set; }

        [Required]
        public bool IsRead { get; set; } = false;

        [Required]
        public bool IsDeletedBySender { get; set; } = false;

        [Required]
        public bool IsDeletedByReceiver { get; set; } = false;

        [ForeignKey(nameof(UserID))]
        public User User { get; set; }

        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    }

    public class LoginAttempt
    {
        [Key]
        public int ID { get; set; }

        [Required]
        public DateTime AttemptTime { get; set; }

        [Required]
        public bool IsSuccessful { get; set; }

        [Required]
        [MaxLength(45)]
        public string IPAddress { get; set; }

        // Mutually Exclusive Foreign Keys
        public int? UserID { get; set; }
        public int? AdminID { get; set; }

        [ForeignKey(nameof(UserID))]
        public virtual User? User { get; set; }

        [ForeignKey(nameof(AdminID))]
        public virtual Admin? Admin { get; set; }

        // Mutual exclusivity logic (not enforced in DB, for validation in app logic)
        [NotMapped]
        public bool IsValid =>
            UserID.HasValue && !AdminID.HasValue ||
            !UserID.HasValue && AdminID.HasValue;
    }

    public enum NotificationType
    {
        [Display(Name = "Account Related")]
        AccountRelated,

        [Display(Name = "Order Status Update")]
        OrderStatusUpdate,

        [Display(Name = "Security Alert")]
        SecurityAlert,

        [Display(Name = "New Message")]
        NewMessage,

        [Display(Name = "Promotion")]
        Promotion,

        [Display(Name = "System Message")]
        SystemMessage
    }

    public static class EnumExtensions
    {
        public static string GetDisplayName(this Enum enumValue)
        {
            var displayAttribute = enumValue.GetType()
                                            .GetMember(enumValue.ToString())[0]
                                            .GetCustomAttribute<DisplayAttribute>();

            return displayAttribute != null ? displayAttribute.Name : enumValue.ToString();
        }
    }

    public class Notification
    {
        [Key]
        public int ID { get; set; }

        public int? UserID { get; set; }
        public int? AdminID { get; set; }
        public int? TicketID { get; set; }
        public int? MessageID { get; set; }

        [Required]
        [StringLength(255)]
        public string Title { get; set; }

        [Required]
        public string Message { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Required]
        public bool IsRead { get; set; } = false;

        [Required]
        public string NotificationType { get; set; }

        // Navigation properties
        [ForeignKey(nameof(AdminID))]
        [InverseProperty("Notifications")]
        public virtual Admin? Admin { get; set; }

        [ForeignKey(nameof(TicketID))]
        public virtual Ticket? Ticket { get; set; }

        [ForeignKey(nameof(MessageID))]
        public virtual Message? MessageForeign { get; set; }

        [ForeignKey(nameof(UserID))]
        [InverseProperty("Notifications")]
        public virtual User? User { get; set; }

        [NotMapped]
        public bool IsValid =>
            UserID.HasValue && !AdminID.HasValue ||
            !UserID.HasValue && AdminID.HasValue;
    }
}
