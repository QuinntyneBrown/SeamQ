using SeamQ.Core.Models;

namespace SeamQ.Core.Abstractions;

public interface IIcdGenerator
{
    Task GenerateAsync(Seam seam, string outputDirectory, IReadOnlyList<string> formats, CancellationToken cancellationToken = default);
}
