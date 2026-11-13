using System.Runtime.CompilerServices;

namespace ForkForward.Mapper.UnitTests;

// Mapping plans are expression trees. Where the runtime can generate code they are compiled; where it cannot
// (Native AOT, or any runtime started with dynamic code switched off) System.Linq.Expressions interprets them.
// CI runs these tests a second time that way: dotnet test -p:DynamicCodeSupport=false.
// The two attributes below mark the tests that cannot pass in that run, and say why.

/// <summary>
/// A fact about something that generates a type at run time with Reflection.Emit: interface proxies and the
/// types projections build for let clauses. Skipped where the runtime cannot generate code.
/// </summary>
public sealed class NeedsDynamicCodeFactAttribute : FactAttribute
{
    public NeedsDynamicCodeFactAttribute()
    {
        if (!RuntimeFeature.IsDynamicCodeSupported)
        {
            Skip = "Generates a type at run time with Reflection.Emit, which this runtime does not support.";
        }
    }
}

/// <summary>
/// A fact that holds when mapping plans are compiled and is known not to hold when they are interpreted.
/// Skipped in the interpreted run, with the reason. The differences are listed in
/// docs/source/Native-AOT-and-trimming.md.
/// </summary>
public sealed class CompiledPlansFactAttribute : FactAttribute
{
    /// <summary>
    /// The catch that wraps an error from a member that can go back into the mapper has a filter. The expression
    /// interpreter fails while evaluating that filter (an IndexOutOfRangeException inside the interpreter, which
    /// counts as "do not catch"), so the error arrives as it was thrown, without the wrapper.
    /// </summary>
    public const string ErrorNotWrapped =
        "Interpreted plans: an error from a member that maps through the context arrives without its AutoMapperMappingException wrapper.";
    public const string ValueTypePath =
        "Interpreted plans: ForPath through fields of nested value types does not set the values.";
    public const string NeedsMoreStack =
        "Interpreted plans: a level of the source graph needs more stack, so this depth does not fit, and errors are not wrapped on the way up.";

    public CompiledPlansFactAttribute(string because)
    {
        if (!RuntimeFeature.IsDynamicCodeCompiled)
        {
            Skip = because;
        }
    }
}

public class InterpretedPlans
{
    class Source
    {
        public int Value { get; set; }
        public Source Child { get; set; }
        public List<Source> Children { get; set; }
    }
    class Destination
    {
        public int Value { get; set; }
        public Destination Child { get; set; }
        public List<Destination> Children { get; set; }
    }

    [Fact]
    public void A_plan_calls_only_methods_that_reflection_can_invoke()
    {
        // An interpreted plan calls its methods through reflection, and reflection cannot invoke a method that is
        // declared on a ref struct or that takes or returns one. The plan used to create the exception for a
        // member mapping error with a static method of TypeMapPlanBuilder, which is a ref struct: interpreted,
        // every such error surfaced as "NotSupportedException: Specified method is not supported".
        var configuration = new MapperConfiguration(cfg => cfg.CreateMap<Source, Destination>());
        var calls = new MethodCalls();

        calls.Visit(configuration.BuildExecutionPlan(typeof(Source), typeof(Destination)));

        calls.Methods.ShouldNotBeEmpty();
        calls.Methods.Where(method =>
                method.DeclaringType.IsByRefLike || method.ReturnType.IsByRefLike || method.GetParameters().Any(p => p.ParameterType.IsByRefLike))
            .Select(method => $"{method.DeclaringType}.{method.Name}")
            .ShouldBeEmpty();
    }

    sealed class MethodCalls : ExpressionVisitor
    {
        public HashSet<MethodInfo> Methods { get; } = [];
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            Methods.Add(node.Method);
            return base.VisitMethodCall(node);
        }
    }

    // The two facts below make the extra CI runs fail when they are not what they claim to be: a run that is
    // meant to interpret the plans, or to be on a newer runtime, must not pass because the switch was lost.
    [Fact]
    public void Plans_are_interpreted_when_the_run_was_asked_to_interpret_them()
    {
        if (Environment.GetEnvironmentVariable("FORKFORWARD_TEST_EXPECT_INTERPRETED") != "true")
        {
            return;
        }
        RuntimeFeature.IsDynamicCodeCompiled.ShouldBeFalse();
    }

    [Fact]
    public void The_run_is_on_the_runtime_it_was_asked_to_be_on()
    {
        var expectedMajor = Environment.GetEnvironmentVariable("FORKFORWARD_TEST_EXPECT_RUNTIME");
        if (string.IsNullOrEmpty(expectedMajor))
        {
            return;
        }
        Environment.Version.Major.ShouldBe(int.Parse(expectedMajor));
    }
}
