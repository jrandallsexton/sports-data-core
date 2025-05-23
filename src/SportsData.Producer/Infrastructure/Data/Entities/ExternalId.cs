﻿using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class ExternalId : CanonicalEntityBase<Guid>
    {
        public string Value { get; set; }

        public SourceDataProvider Provider { get; set; }
    }
}
