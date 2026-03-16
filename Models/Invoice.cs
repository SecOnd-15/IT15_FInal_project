using System;
using System.ComponentModel.DataAnnotations;

namespace Latog_Final_project.Models
{
    public class Invoice
    {
        [Key]
        public int Id { get; set; }

        public int BranchId { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.ForeignKey("BranchId")]
        public virtual Branch? Branch { get; set; }

        public int ResourceRequestId { get; set; }

        public string Supplier { get; set; } = string.Empty;

        public decimal TotalAmount { get; set; }

        public string PaymentStatus { get; set; } = "Unpaid"; // "Unpaid", "Partially Paid", or "Paid"

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime? PaidDate { get; set; }

        public DateTime? DueDate { get; set; }

        public string InvoiceNumber { get; set; } = string.Empty;

        public string? Notes { get; set; }

        public string? AttachmentPath { get; set; }

        public decimal AmountPaid { get; set; } = 0;

        public virtual ResourceRequest? ResourceRequest { get; set; }
    }
}
