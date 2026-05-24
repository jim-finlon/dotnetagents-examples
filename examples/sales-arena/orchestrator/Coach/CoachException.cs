namespace SalesArena.Orchestrator.Coach;

public enum CoachErrorCode
{
    SpeechEmpty = 1,
    SpeechTooLong = 2,
    SpeechContainsControlCharacters = 3,
    SpeechContainsPromptInjectionMarker = 4,
    PersonaNotActive = 5,
    OperatorRequired = 6,
    ExpiresAfterMustBePositive = 7,
}

public sealed class CoachException : Exception
{
    public CoachErrorCode Code { get; }
    public CoachException(CoachErrorCode code, string message) : base(message)
    {
        Code = code;
    }
}
