#if NETSTANDARD2_0
// The only marker the compiler cannot do without here: record types generate
// init-only setters, and the emitted reference to this attribute has to resolve
// to something. .NET 5 and later ship it in the framework. Everything else the
// library needs on netstandard2.0 is written in plain C# rather than polyfilled.
namespace System.Runtime.CompilerServices
{
    using System.ComponentModel;

    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit { }
}
#endif
