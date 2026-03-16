using System;
using System.Collections.Generic;

namespace Latog_Final_project.Models
{
    public class InventoryTransactionViewModel
    {
        public List<InventoryTransaction> Transactions { get; set; } = new();
        
        // Pagination
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalRecords { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalRecords / PageSize);
        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;
    }
}
