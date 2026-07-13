namespace AstilCodex.Contracts;

public sealed record AvatarStateEvent(
    AvatarState State,
    string Detail,
    DateTimeOffset Timestamp)
{
    public static AvatarStateEvent Now(AvatarState state, string detail) =>
        new(state, detail, DateTimeOffset.UtcNow);
}
