using System;
﻿namespace SportsData.Core.Dtos.Canonical
{
    public class ConferenceDivisionNameAndSlugDto
    {
        public required string Division { get; set; }

        public required string ShortName { get; set; }

        public required string Slug { get; set; }
    }
}
