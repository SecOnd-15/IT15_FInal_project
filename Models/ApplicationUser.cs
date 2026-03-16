using Microsoft.AspNetCore.Identity;

namespace Latog_Final_project.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? FullName { get; set; }

        public int? BranchId { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.ForeignKey("BranchId")]
        public virtual Branch? Branch { get; set; }
    }
}
