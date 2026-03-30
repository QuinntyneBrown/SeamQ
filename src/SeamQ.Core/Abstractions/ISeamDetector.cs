using SeamQ.Core.Models;

namespace SeamQ.Core.Abstractions;

public interface ISeamDetector
{
    Task<IReadOnlyList<Seam>> DetectAsync(IReadOnlyList<Workspace> workspaces, CancellationToken cancellationToken = default);
}
