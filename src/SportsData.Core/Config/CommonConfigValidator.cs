using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace SportsData.Core.Config
{
    /// <summary>
    /// Validates CommonConfig based on messaging transport selection.
    /// Ensures AzureServiceBusConnectionString is present when RabbitMQ is not enabled.
    /// </summary>
    public class CommonConfigValidator : IValidateOptions<CommonConfig>
    {
        private readonly IConfiguration _configuration;

        public CommonConfigValidator(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public ValidateOptionsResult Validate(string? name, CommonConfig options)
        {
            var useRabbitMq = _configuration.GetValue<bool>(CommonConfigKeys.MessagingUseRabbitMq);

            // If RabbitMQ is not enabled, Azure Service Bus connection string is required
            if (!useRabbitMq && string.IsNullOrWhiteSpace(options.AzureServiceBusConnectionString))
            {
                return ValidateOptionsResult.Fail(
                    $"Azure Service Bus is the selected messaging transport ('{CommonConfigKeys.MessagingUseRabbitMq}' is false), " +
                    $"but '{CommonConfigKeys.AzureServiceBus}' is not configured. " +
                    "Please set the Azure Service Bus connection string in configuration or enable RabbitMQ.");
            }

            return ValidateOptionsResult.Success;
        }
    }
}
