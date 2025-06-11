using System;

namespace SportsData.Core.Common.Hashing
{
    public interface IHasSourceUrl : IHasSourceUrlHash
    {
        Uri Uri { get; set; }
    }

    public interface IHasSourceUrlInitOnly : IHasSourceUrlHashInitOnly
    {
        Uri Uri { get; init; }
    }
}
