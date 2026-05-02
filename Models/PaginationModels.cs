using System.Collections.Generic;

namespace BizSuite.Models
{
    public class PagedResult<T>
    {
        public IEnumerable<T> Items { get; set; } = new List<T>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => TotalCount == 0 ? 0 : (TotalCount - 1) / PageSize + 1;
    }

    public class SalesOrderQueryParameters
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public string SearchTerm { get; set; } = string.Empty;
        public string FulfillmentStatus { get; set; } = string.Empty;
        public string SortBy { get; set; } = "OrderDate";
        public string SortDirection { get; set; } = "DESC";
    }
}
