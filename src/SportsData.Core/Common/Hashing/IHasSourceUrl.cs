namespace SportsData.Core.Common.Hashing
{
    public interface IHasSourceUrl : IHasSourceUrlHash
    {
        string Url { get; set; }
    }

    public interface IHasSourceUrlInitOnly : IHasSourceUrlHashInitOnly
    {
        string Url { get; init; }
    }
}
