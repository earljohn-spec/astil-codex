using AstilCodex.Contracts;

namespace AstilCodex.Core.Routing;

public interface ITaskRouter
{
    TaskManifest Route(
        TaskRequest task,
        ProcessingPolicy policy,
        HardwareProfile hardware);
}
