using System;

namespace SportsData.Core.Common
{
    public static class CausationId
    {
        public static class Producer
        {
            public static Guid FranchiseDocumentProcessor = new Guid("0CF09E42-B9B1-42AB-9027-51CECF2D19D6");
            public static Guid VenueCreatedDocumentProcessor = new Guid("0DF09E42-B9B1-42AB-9027-51CECF2D19D6");
            public static Guid GroupBySeasonDocumentProcessor = new Guid("0EF09E42-B9B1-42AB-9027-51CECF2D19D6");
            public static Guid TeamSeasonDocumentProcessor = new Guid("0FF09E42-B9B1-42AB-9027-51CECF2D19D6");
            public static Guid ImageRequestedProcessor = new Guid("10F09E42-B9B1-42AB-9027-51CECF2D19D6");
        }

        public static class Provider
        {
            public static Guid ResourceIndexItemProcessor = new Guid("3F3E7C5B-CCAD-4BEE-9696-280D9FBE34BA");
        }
    }
}
