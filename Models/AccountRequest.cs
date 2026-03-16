using System;
using System.ComponentModel.DataAnnotations;

namespace Latog_Final_project.Models
{
    public class AccountRequest
    {
        public int Id { get; set; }

        public int? BranchId { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.ForeignKey("BranchId")]
        public virtual Branch? Branch { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        public string RequestedRole { get; set; } = "Staff";

        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected

        public DateTime DateRequested { get; set; } = DateTime.Now;
    }
}
