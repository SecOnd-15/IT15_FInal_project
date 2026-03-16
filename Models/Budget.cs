using System.ComponentModel.DataAnnotations;

namespace Latog_Final_project.Models
{
    public class Budget
    {
        [Key]
        public int Id { get; set; }

        public int BranchId { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.ForeignKey("BranchId")]
        public virtual Branch? Branch { get; set; }

        [Required]
        public string CategoryName { get; set; } = string.Empty;

        public decimal MonthlyLimit { get; set; }

        public decimal CurrentSpending { get; set; }

    }
}
