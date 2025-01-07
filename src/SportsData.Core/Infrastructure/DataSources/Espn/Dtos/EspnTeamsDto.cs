using System.Collections.Generic;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos
{
    public class EspnTeamsDto
    {
        public int count { get; set; }
        public int pageIndex { get; set; }
        public int pageSize { get; set; }
        public int pageCount { get; set; }
        public List<EspnTeamsItem> items { get; set; }
    }

    public class EspnTeamsItem
    {
        public string href { get; set; }
    }
}
