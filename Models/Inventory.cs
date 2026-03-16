using System.ComponentModel.DataAnnotations;

namespace Latog_Final_project.Models
{
    public class Inventory
    {
        [Key]
        public int Id { get; set; }

        public int BranchId { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.ForeignKey("BranchId")]
        public virtual Branch? Branch { get; set; }

        public string ItemName { get; set; } = string.Empty;

        public int Quantity { get; set; }
    }
}
