using AutoMapper;

using System;
using System.Linq;
using System.Reflection;

namespace SportsData.Core.Common.Mapping
{
    public class DynamicMappingProfile : Profile
    {
        public DynamicMappingProfile()
        {
            //ApplyMappingsFromAssembly(typeof(Program).Assembly);
            ApplyMappingsFromAssembly(Assembly.GetExecutingAssembly());
            ApplyMappingsFromAssembly(Assembly.GetCallingAssembly());
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
