using Fleece.Core.FunctionalCore;
using Fleece.Core.Services.Interfaces;

namespace Fleece.Core.Services;

internal sealed class GuidIdGenerator : IIdGenerator
{
    public string Generate()
    {
        return IdGeneration.Generate();
    }
}
