﻿using MassTransit;

using SportsData.Core.Eventing.Events.Images;
using SportsData.Core.Processing;

namespace SportsData.Producer.Application.Images.Handlers
{
    public class ProcessImageResponseHandler :
        IConsumer<ProcessImageResponse>
    {
        private readonly ILogger<ProcessImageResponseHandler> _logger;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public ProcessImageResponseHandler(
            ILogger<ProcessImageResponseHandler> logger,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _logger = logger;
            _backgroundJobProvider = backgroundJobProvider;
        }

        public Task Consume(ConsumeContext<ProcessImageResponse> context)
        {
            using (_logger.BeginScope(new Dictionary<string, object>
                   {
                       ["CorrelationId"] = context.Message.CorrelationId
                   }))
            {
                _logger.LogInformation("new ProcessImageResponse event received: {@message}", context.Message);
                _backgroundJobProvider.Enqueue<ImageProcessedProcessor>(x => x.Process(context.Message));
            }
            return Task.CompletedTask;
        }
    }
}
