using System;
using System.ComponentModel.DataAnnotations;

namespace Latog_Final_project.Models
{
    public class Branch
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Barangay Name")]
        public string BranchName { get; set; } = string.Empty;


        public string? Location { get; set; }
        public string? ContactNumber { get; set; }


        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public bool IsActive { get; set; } = true;
    }
}
