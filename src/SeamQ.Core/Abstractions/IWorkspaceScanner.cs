using SeamQ.Core.Models;

namespace SeamQ.Core.Abstractions;

public interface IWorkspaceScanner
{
    Task<Workspace> ScanAsync(string workspacePath, CancellationToken cancellationToken = default);
}
