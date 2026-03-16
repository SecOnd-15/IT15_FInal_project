using System;
using System.ComponentModel.DataAnnotations;

namespace Latog_Final_project.Models
{
    public class InventoryTransaction
    {
        [Key]
        public int Id { get; set; }

        public int BranchId { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.ForeignKey("BranchId")]
        public virtual Branch? Branch { get; set; }

        [Required]
        public string ItemName { get; set; } = string.Empty;

        [Required]
        public string TransactionType { get; set; } = string.Empty; // "Stock-In", "Stock-Out", "Adjustment"

        public int Quantity { get; set; }

        public int PreviousStock { get; set; }

        public int NewStock { get; set; }

        public string? Reason { get; set; }

        public string PerformedBy { get; set; } = string.Empty;

        public DateTime TransactionDate { get; set; } = DateTime.Now;

        public bool IsArchived { get; set; } = false;
        public DateTime? ArchivedDate { get; set; }
    }
}
