using SportsData.Core.Common;

using System;
using System.Collections.Generic;

namespace SportsData.Core.Models.Canonical
{
    public class CanonicalModelBase
    {
        public string Id { get; set; }

        public Dictionary<SourceDataProvider, string> ExternalIds { get; set; } = new Dictionary<SourceDataProvider, string>();

        public DateTime CreatedUtc { get; set; }

        public DateTime? UpdatedUtc { get; set; }
    }
}
