namespace MyAIAgent.Orchestrator.Application.Common.Exceptions;

// Lets Application react to a failed save without referencing the persistence provider.
public sealed class PersistenceException(string message, Exception innerException)
    : Exception(message, innerException);
