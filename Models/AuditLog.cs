using System;
using System.ComponentModel.DataAnnotations;

namespace Latog_Final_project.Models
{
    public class AuditLog
    {
        [Key]
        public int Id { get; set; }

        public int? BranchId { get; set; }

        public string UserId { get; set; } = string.Empty;

        [Required]
        public string Action { get; set; } = string.Empty;

        [Required]
        public string Module { get; set; } = string.Empty;

        public string EntityName { get; set; } = string.Empty;

        public string EntityId { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; } = DateTime.Now;

        public string Details { get; set; } = string.Empty;
    }
}
