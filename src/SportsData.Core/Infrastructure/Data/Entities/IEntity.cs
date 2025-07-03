using System;

namespace SportsData.Core.Infrastructure.Data.Entities
{
    public interface IEntity<T>
    {
        public T? Id { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? ModifiedUtc { get; set; }
        public Guid CreatedBy { get; set; }
        public Guid? ModifiedBy { get; set; }
    }
}
