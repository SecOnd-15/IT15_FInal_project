using System;
using System.Collections.Generic;

namespace Latog_Final_project.Models
{
    public class AuditLogFilterViewModel
    {
        // Filter inputs
        public string? Role { get; set; }
        public string? Action { get; set; }

        // Results
        public List<AuditLog> Logs { get; set; } = new();

        // Dropdown options
        public List<string> Roles { get; set; } = new();
        public List<string> Actions { get; set; } = new();
        public List<UserOption> Users { get; set; } = new();

        // Pagination
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 15;
        public int TotalRecords { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalRecords / PageSize);
        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;
    }

    public class UserOption
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}
