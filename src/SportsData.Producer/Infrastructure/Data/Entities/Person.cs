using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class Person : CanonicalEntityBase<Guid>
    {
        public string LastName { get; set; }

        public string FirstName { get; set; }

        public string Title { get; set; }

        public string Nickname { get; set; }

        public int Experience { get; set; }
    }
}
