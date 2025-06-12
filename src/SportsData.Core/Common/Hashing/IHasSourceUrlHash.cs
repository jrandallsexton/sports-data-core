namespace SportsData.Core.Common.Hashing
{
    public interface IHasSourceUrlHash
    {
        string SourceUrlHash { get; set; }
    }

    public interface IHasSourceUrlHashInitOnly
    {
        string SourceUrlHash { get; init; }
    }
}
