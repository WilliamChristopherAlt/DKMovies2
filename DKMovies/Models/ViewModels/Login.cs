using System.ComponentModel.DataAnnotations;

namespace DKMovies.Models.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Please enter username or email")]
        [Display(Name = "Username or Email")]
        public string Username { get; set; }

        [Required(ErrorMessage = "Please enter password")]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }
    }

    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Please enter your full name")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Please enter username")]
        [Display(Name = "Username")]
        public string Username { get; set; }

        [Required(ErrorMessage = "Please enter email")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Please enter password")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Please confirm your password")]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        [Display(Name = "Confirm Password")]
        public string ConfirmPassword { get; set; }

        [Display(Name = "Phone Number")]
        public string Phone { get; set; }

        [Display(Name = "Birth Date")]
        public DateTime? BirthDate { get; set; }

        [Required(ErrorMessage = "Please select gender")]
        [Display(Name = "Gender")]
        public string Gender { get; set; }

        [Range(typeof(bool), "true", "true", ErrorMessage = "You must accept the terms and conditions")]
        [Display(Name = "I agree to the terms and conditions")]
        public bool TermsAccepted { get; set; }
    }
}