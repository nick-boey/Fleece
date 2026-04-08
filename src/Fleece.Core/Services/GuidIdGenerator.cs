using Fleece.Core.FunctionalCore;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Core.Services;

public sealed class GuidIdGenerator : IIdGenerator
{
    public string Generate() => IdGeneration.Generate();
}
