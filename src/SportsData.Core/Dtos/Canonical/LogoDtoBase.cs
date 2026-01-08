using System;

namespace SportsData.Core.Dtos.Canonical
{
    public abstract class LogoDtoBase
    {
        public Uri Url { get; init; } = default!;

        public int? Height { get; init; }

        public int? Width { get; init; }
        
        protected LogoDtoBase()
        {
        }
        
        protected LogoDtoBase(Uri url, int? height, int? width)
        {
            Url = url;
            Height = height;
            Width = width;
        }
    }
}
