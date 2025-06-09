#pragma warning disable CS8618 // Non-nullable property is uninitialized

using System.Collections.Generic;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos
{
    public class EspnResourceIndexDto
    {
        public int Count { get; set; }

        public int PageIndex { get; set; }

        public int PageSize { get; set; }

        public int PageCount { get; set; }

        public List<EspnResourceIndexItem> Items { get; set; } = new();
    }
}
