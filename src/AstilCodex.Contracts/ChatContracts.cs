namespace AstilCodex.Contracts;

public sealed record ChatMessage(
    string Role,
    string Content,
    DateTimeOffset CreatedAt);

public sealed record ChatRequest(
    string SessionId,
    AssistantMode Mode,
    string UserText,
    IReadOnlyList<ChatMessage> History);

public sealed record ChatResult(
    string Text,
    TaskManifest Manifest,
    TimeSpan Duration,
    string ProviderId);
