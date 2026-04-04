using Fleece.Core.FunctionalCore;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Core.Services;

public sealed class TagService : ITagService
{
    public string? ValidateTag(string tag) => Tags.ValidateTag(tag);

    public IReadOnlyList<string> ValidateTags(IReadOnlyList<string>? tags) => Tags.ValidateTags(tags);

    public (string Key, string? Value) ParseTag(string tag) => Tags.ParseTag(tag);

    public bool HasKeyedTag(Issue issue, string key, string value) => Tags.HasKeyedTag(issue, key, value);

    public bool HasTagKey(Issue issue, string key) => Tags.HasTagKey(issue, key);

    public IReadOnlyDictionary<string, IReadOnlyList<string>> GetKeyedTags(Issue issue) => Tags.GetKeyedTags(issue);
}
