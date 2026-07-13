namespace AstilCodex.Contracts;

public sealed record HardwareProfile(
    bool LocalModelAvailable,
    bool CloudProviderAvailable,
    bool LocalHighComplexityCapable)
{
    public static HardwareProfile Development => new(
        LocalModelAvailable: true,
        CloudProviderAvailable: true,
        LocalHighComplexityCapable: false);
}
