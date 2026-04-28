using System.Runtime.CompilerServices;
using VerifyTests;

namespace Fleece.Core.Tests.Services.GraphLayout;

internal static class VerifyInit
{
    [ModuleInitializer]
    public static void Init()
    {
        VerifierSettings.DisableRequireUniquePrefix();
        DerivePathInfo((sourceFile, projectDirectory, type, method) => new PathInfo(
            directory: Path.Combine(Path.GetDirectoryName(sourceFile)!, "Snapshots"),
            typeName: type.Name,
            methodName: method.Name));
    }
}
