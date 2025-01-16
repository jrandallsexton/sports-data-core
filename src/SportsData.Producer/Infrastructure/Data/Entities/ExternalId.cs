﻿using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class FranchiseExternalId : ExternalId
    {

    }
    public class VenueExternalId : ExternalId
    {

    }

    public class ExternalId : EntityBase<Guid>
    {
        public string Value { get; set; }

        public SourceDataProvider Provider { get; set; }
    }
}
