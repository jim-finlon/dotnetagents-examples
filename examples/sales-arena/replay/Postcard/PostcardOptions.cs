namespace SalesArena.Replay.Postcard;

/// <summary>Options for <see cref="IPostcardGenerator.Generate"/>.</summary>
public sealed record PostcardOptions(
    PostcardStyle Style = PostcardStyle.Vintage,
    string? ContestDisplayName = null,
    string? Catchphrase = null,
    int Width = 800,
    int Height = 500);
