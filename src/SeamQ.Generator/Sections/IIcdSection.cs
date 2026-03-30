using SeamQ.Core.Models;

namespace SeamQ.Generator.Sections;

public interface IIcdSection
{
    string Title { get; }
    int Order { get; }
    Task<string> GenerateAsync(Seam seam, CancellationToken cancellationToken = default);
}
