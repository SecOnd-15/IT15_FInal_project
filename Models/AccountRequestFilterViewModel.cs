using System;
using System.Collections.Generic;

namespace Latog_Final_project.Models
{
    public class AccountRequestFilterViewModel
    {
        public string? StatusFilter { get; set; }
        
        public List<AccountRequest> Requests { get; set; } = new();

        // Pagination
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalRecords { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalRecords / PageSize);
        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;

        // AJAX Metadata
        public string ControllerName { get; set; } = "AccountRequest";
        public string ActionName { get; set; } = "Index";
    }
}
