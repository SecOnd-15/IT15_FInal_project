using System.ComponentModel.DataAnnotations;

namespace Latog_Final_project.Models
{
    public class CreateBarangayViewModel
    {
        // Barangay Details
        [Required]
        [Display(Name = "Barangay Name")]
        public string BranchName { get; set; } = string.Empty;

        public string? Location { get; set; }

        [Display(Name = "Contact Number")]
        public string? ContactNumber { get; set; }

        // Chairman Account Details
        [Required]
        [EmailAddress]
        [Display(Name = "Chairman Gmail/Email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
