using Fleece.Core.FunctionalCore;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Core.Services;

public sealed class Sha256IdGenerator : IIdGenerator
{
    public string Generate(string title) => IdGeneration.Generate(title);

    public string Generate(string title, int salt) => IdGeneration.Generate(title, salt);
}
