using System;
using System.Collections.Generic;

namespace FlexiSpace.Core.Common
{
    // Generic paged-list wrapper returned by search/filter endpoints.
    public class PagedResult<T>
    {
        public IReadOnlyList<T> Items { get; set; } = new List<T>();

        public int TotalCount { get; set; }

        public int Page { get; set; }

        public int PageSize { get; set; }

        public int TotalPages => PageSize <= 0
            ? 0
            : (int)Math.Ceiling(TotalCount / (double)PageSize);
    }
}
