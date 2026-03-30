using SeamQ.Core.Models;

namespace SeamQ.Core.Abstractions;

public interface IDataExporter
{
    Task<IReadOnlyList<string>> ExportAsync(Seam seam, string outputDirectory, string format, CancellationToken cancellationToken = default);
}
