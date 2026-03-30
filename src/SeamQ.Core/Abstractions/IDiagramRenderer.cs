using SeamQ.Core.Models;

namespace SeamQ.Core.Abstractions;

public interface IDiagramRenderer
{
    Task<IReadOnlyList<string>> RenderAsync(Seam seam, string outputDirectory, CancellationToken cancellationToken = default);
}
