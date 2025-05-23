﻿using AutoMapper;

using SportsData.Core.Common.Mapping;

using System.Reflection;

namespace SportsData.Producer
{
    public class DynamicMappingProfile : Profile
    {
        public DynamicMappingProfile()
        {
            //ApplyMappingsFromAssembly(typeof(Program).Assembly);
            ApplyMappingsFromAssembly(Assembly.GetExecutingAssembly());
        }

        private void ApplyMappingsFromAssembly(Assembly assembly)
        {
            var types = assembly.GetExportedTypes()
                .Where(t => t.GetInterfaces().Any(i =>
                    i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IMapFrom<>)))
                .ToList();

            foreach (var type in types)
            {
                var instance = Activator.CreateInstance(type);

                var methodInfo = type.GetMethod("Mapping");

                methodInfo?.Invoke(instance, new object[] { this });
            }
        }
    }
}
