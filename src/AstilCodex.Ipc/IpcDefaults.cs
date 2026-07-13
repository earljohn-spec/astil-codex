namespace AstilCodex.Ipc;

public static class IpcDefaults
{
    public const string PipeName = "astil-codex-core-v1";
    public static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(5);
}
