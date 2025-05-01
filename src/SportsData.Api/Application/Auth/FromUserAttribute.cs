using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace SportsData.Api.Application.Auth
{
    [AttributeUsage(AttributeTargets.Parameter)]
    public class FromUserAttribute : Attribute, IBindingSourceMetadata
    {
        public BindingSource BindingSource => BindingSource.Custom;
    }
}
