using System;

namespace SportsData.Core.Infrastructure.Data.Entities
{
    public interface IEntity<T>
    {
        T Id { get; set; }
        DateTime CreatedUtc { get; set; }
        DateTime? ModifiedUtc { get; set; }
        Guid CreatedBy { get; set; }
        Guid? ModifiedBy { get; set; }
    }
}
