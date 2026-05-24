namespace SalesArena.Tournament;

public enum TournamentErrorCode
{
    NotEnoughPersonas = 1,
    TooManyPersonas = 2,
    DuplicatePersona = 3,
    BracketAlreadyComplete = 4,
    UnknownRound = 5,
    UnknownMatch = 6,
    MatchAlreadyDecided = 7,
    WinnerNotInMatch = 8,
}

public sealed class TournamentException : Exception
{
    public TournamentErrorCode Code { get; }
    public TournamentException(TournamentErrorCode code, string message) : base(message)
    {
        Code = code;
    }
}
