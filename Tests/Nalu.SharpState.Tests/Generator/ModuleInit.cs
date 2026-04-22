using System.Runtime.CompilerServices;

namespace Nalu.SharpState.Tests.Generator;

public static class ModuleInit
{
    [ModuleInitializer]
    public static void Init()
    {
        VerifySourceGenerators.Initialize();
    }
}
