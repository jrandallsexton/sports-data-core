using System;

namespace SportsData.Core.Dtos.Canonical
{
    public abstract class LogoDtoBase
    {
        public required Uri Url { get; init; }

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
