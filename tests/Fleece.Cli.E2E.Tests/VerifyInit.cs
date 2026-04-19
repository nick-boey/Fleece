using System.Runtime.CompilerServices;

namespace Fleece.Cli.E2E.Tests;

internal static class VerifyInit
{
    [ModuleInitializer]
    public static void Init()
    {
        VerifierSettings.DisableRequireUniquePrefix();
        DerivePathInfo((sourceFile, projectDirectory, type, method) => new PathInfo(
            directory: Path.Combine(projectDirectory, "Snapshots"),
            typeName: type.Name,
            methodName: method.Name));
    }
}
