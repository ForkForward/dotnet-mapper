namespace ForkForward.Mapper.Internal;

/// <summary>
/// What the build tells an application that is trimmed or compiled ahead of time when it creates a mapper
/// configuration: the texts of the RequiresUnreferencedCode and RequiresDynamicCode attributes on the entry points.
/// </summary>
/// <remarks>
/// The library is not compatible with trimming or Native AOT. Without these attributes such an application builds
/// without a word about it and fails when it starts; with them the build warns at the line that creates the
/// configuration. An application that is neither trimmed nor compiled ahead of time is not affected.
/// </remarks>
static class TrimmingAndAot
{
    public const string Url = "https://forkforward.dev/dotnet-mapper/Native-AOT-and-trimming/";
    public const string Trimming =
        "ForkForward.Mapper finds the members of the mapped types, and methods of its own, through reflection, and trimming can remove them.";
    public const string DynamicCode =
        "ForkForward.Mapper builds mapping plans as expression trees and closes generic types and methods at run time. Without dynamic code the plans are interpreted, proxies cannot be generated, and the code for a generic instantiation may be missing.";
}
