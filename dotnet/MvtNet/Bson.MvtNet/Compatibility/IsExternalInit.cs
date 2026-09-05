#if NETSTANDARD2_0
// Record types generate init-only setters, and the compiler emits a reference to
// this attribute for them. .NET 5 and later ship it; netstandard2.0 does not.
namespace System.Runtime.CompilerServices
{
    using System.ComponentModel;

    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit { }
}
#endif
