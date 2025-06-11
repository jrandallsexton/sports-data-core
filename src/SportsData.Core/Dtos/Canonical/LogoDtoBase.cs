using System;

namespace SportsData.Core.Dtos.Canonical
{
    public abstract class LogoDtoBase(Uri url, int? height, int? width)
    {
        public Uri Url { get; init; } = url;

        public int? Height { get; init; } = height;

        public int? Width { get; init; } = width;
    }
}
