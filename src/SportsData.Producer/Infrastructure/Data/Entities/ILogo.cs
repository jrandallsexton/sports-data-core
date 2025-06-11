namespace SportsData.Producer.Infrastructure.Data.Entities;

public interface ILogo
{
    public string OriginalUrlHash { get; set; }

    public Uri Uri { get; set; }

    public long? Height { get; set; }

    public long? Width { get; set; }

    public List<string>? Rel { get; set; }
}