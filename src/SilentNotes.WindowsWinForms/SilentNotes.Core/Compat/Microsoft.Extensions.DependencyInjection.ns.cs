// Compat shim for .NET 4.0: Ioc.cs (frozen source) carries a "using
// Microsoft.Extensions.DependencyInjection;" directive, but on net40 the Microsoft.Extensions
// packages are not available and the namespace would not resolve (CS0246). No types of that
// namespace are actually referenced, so an empty namespace declaration is enough.
namespace Microsoft.Extensions.DependencyInjection
{
}
