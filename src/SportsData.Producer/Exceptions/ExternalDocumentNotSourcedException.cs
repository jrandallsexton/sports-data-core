namespace SportsData.Producer.Exceptions
{
    // TODO: Remove once retry logic is refactored to explicit deferral.
    public class ExternalDocumentNotSourcedException(string message) : Exception(message);
}
