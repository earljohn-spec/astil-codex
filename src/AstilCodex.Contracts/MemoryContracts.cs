namespace AstilCodex.Contracts;

public sealed record ConversationSession(
    string SessionId,
    AssistantMode Mode,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record StoredChatMessage(
    long MessageId,
    string SessionId,
    string Role,
    string Content,
    DateTimeOffset CreatedAt);
