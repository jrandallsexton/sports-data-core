using MassTransit;

namespace SportsData.Producer.Application.Competitions.InstantAnalysis;

/// <summary>
/// Gives Producer's <see cref="FootballPlayCompletedConsumer"/> its own queue.
/// By default the kebab-case formatter would name it
/// "football-play-completed", the same queue as the API's consumer of this
/// event, and on a shared broker (the local stack) each play would reach only
/// one of them, starving the API's live last-play updates.
/// </summary>
public class FootballPlayCompletedConsumerDefinition : ConsumerDefinition<FootballPlayCompletedConsumer>
{
    public const string QueueName = "producer-football-play-completed";

    public FootballPlayCompletedConsumerDefinition()
    {
        EndpointName = QueueName;
    }
}
