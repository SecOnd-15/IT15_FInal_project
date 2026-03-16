using System;
using System.ComponentModel.DataAnnotations;

namespace Latog_Final_project.Models
{
    public class ResourceRequest
    {
        [Key]
        public int Id { get; set; }

        public int BranchId { get; set; }
        
        [System.ComponentModel.DataAnnotations.Schema.ForeignKey("BranchId")]
        public virtual Branch? Branch { get; set; }

        public string UserId { get; set; } = string.Empty;
        
        [System.ComponentModel.DataAnnotations.Schema.ForeignKey("UserId")]
        public virtual ApplicationUser? User { get; set; }

        public string? ForwardedByUserId { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.ForeignKey("ForwardedByUserId")]
        public virtual ApplicationUser? ForwardedByUser { get; set; }

        public bool HasPurchaseOrder { get; set; }

        public decimal? TotalAmount { get; set; }

        public string? Category { get; set; }
        public decimal? EstimatedAmount { get; set; }

        // 🔥 ADD THIS
        public string? Supplier { get; set; }
        public int? ReceivedQuantity { get; set; }
        [Required]
        public string ItemName { get; set; } = string.Empty;

        public int Quantity { get; set; }

        [Required]
        public string Purpose { get; set; } = string.Empty;

        public string Status { get; set; } = "Pending";

        public DateTime RequestDate { get; set; }

        public DateTime? DecisionDate { get; set; }

        public bool IsArchived { get; set; } = false;

        public DateTime? ArchivedDate { get; set; }

    }
}
