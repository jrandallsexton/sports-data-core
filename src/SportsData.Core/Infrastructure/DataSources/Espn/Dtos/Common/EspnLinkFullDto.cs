using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

public class EspnLinkFullDto
{
    [JsonProperty("language")]
    public string Language { get; set; }

    [JsonProperty("rel")]
    public List<string> Rel { get; set; }

    [JsonProperty("href")]
    public Uri Href { get; set; }

    [JsonProperty("text")]
    public string Text { get; set; }

    [JsonProperty("shortText")]
    public string ShortText { get; set; }

    [JsonProperty("isExternal")]
    public bool IsExternal { get; set; }

    [JsonProperty("isPremium")]
    public bool IsPremium { get; set; }
}