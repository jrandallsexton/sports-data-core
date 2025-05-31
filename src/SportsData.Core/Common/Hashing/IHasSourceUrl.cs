namespace SportsData.Core.Common.Hashing
{
    public interface IHasSourceUrl : IHasSourceUrlHash
    {
        string Url { get; set; }
    }
}
