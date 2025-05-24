namespace SportsData.Core.Models.Canonical
{
    public abstract class LogoDtoBase(string url, int? height, int? width)
    {
        public string Url { get; init; } = url;

        public int? Height { get; init; } = height;

        public int? Width { get; init; } = width;
    }
}
