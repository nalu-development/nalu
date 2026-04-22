#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    /// <summary>Polyfill for C# 9 init-only properties on netstandard2.0.</summary>
    internal static class IsExternalInit
    {
    }
}
#endif
