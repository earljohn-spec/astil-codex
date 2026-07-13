using AstilCodex.Contracts;

namespace AstilCodex.Core.Permissions;

public interface IPermissionBroker
{
    PermissionResult Evaluate(IEnumerable<string> requestedTools);
}
