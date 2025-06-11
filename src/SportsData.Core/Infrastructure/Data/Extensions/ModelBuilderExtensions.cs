using System;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace SportsData.Core.Infrastructure.Data.Extensions
{
    public static class ModelBuilderExtensions
    {
        public static ModelBuilder WithUriConverter(this ModelBuilder modelBuilder)
        {
            var uriConverter = new ValueConverter<Uri, string>(
                uri => uri.ToString(),
                str => new Uri(str, UriKind.Absolute));

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(Uri))
                    {
                        property.SetValueConverter(uriConverter);
                    }
                }
            }

            return modelBuilder;
        }
    }
}