using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using System;

namespace SportsData.Core.Infrastructure.Data;

public class UriToStringConverter : ValueConverter<Uri, string>
{
    public UriToStringConverter() : base(
        uri => uri.ToString(),
        str => new Uri(str, UriKind.Absolute))
    { }
}