namespace SportsData.Core.Common.Hashing
{
    public interface IHasSourceUrlHash
    {
        string UrlHash { get; set; }
    }

    public interface IHasSourceUrlHashInitOnly
    {
        string UrlHash { get; init; }
    }
}
