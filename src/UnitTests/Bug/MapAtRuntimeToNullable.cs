namespace ForkForward.Mapper.UnitTests.Bug;

// A member that is not inlined into the plan of its type map is mapped by a call through the context: a member with
// MapAtRuntime, or one whose own type map preserves references or lies beyond MaxExecutionPlanDepth.
// A value that comes from a chain (members, instance method calls, extension method calls) is null checked link by
// link, and when it is a T that goes into a T?, the checked chain is a T?, so that a null on the way maps to null.
// The call through the context was still made for a T, and the plan could not be built:
// "Expression of type 'System.Nullable`1[System.Int32]' cannot be used for parameter of type 'System.Int32' of method
// 'System.Nullable`1[System.Int32] MapInternal[Int32,Nullable`1](Int32, System.Nullable`1[System.Int32], MemberMap)'".
// A static method call that is not an extension method call is not a link of a chain, which is why it always worked.
public class MapAtRuntimeToNullable
{
    public enum Color { Red, Green, Blue }
    public readonly record struct Point(int X, int Y);
    public class Source
    {
        public string Text { get; set; }
        public int Number { get; set; }
        public int? NullableNumber { get; set; }
        public Color Color { get; set; }
        public Point Point { get; set; }
        public Inner Inner { get; set; }
    }
    public class Inner
    {
        public string Text { get; set; }
        public int Number { get; set; }
        public int? NullableNumber { get; set; }
        public Color Color { get; set; }
        public Point Point { get; set; }
        public Innermost Innermost { get; set; }
        public int GetNumber() => Number;
        public Color GetColor() => Color;
        public Point GetPoint() => Point;
    }
    public class Innermost
    {
        public int Number { get; set; }
    }
    public class Destination<TMember>
    {
        public TMember Value { get; set; }
    }
    // The methods of MapAtRuntimeToNullableExtensions, called as what they are without "this".
    public static class Parse
    {
        public static int ToInt(string text) => int.Parse(text);
        public static Color ToColor(string text) => Enum.Parse<Color>(text);
        public static Point ToPoint(string text) => new(int.Parse(text), 0);
    }
    static readonly bool[] InlinedAndAtRuntime = [false, true];

    static IMapper MapperFor<TMember>(bool atRuntime, Action<IMemberConfigurationExpression<Source, Destination<TMember>, TMember>> member,
        Action<IMapperConfigurationExpression> configure = null)
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            configure?.Invoke(cfg);
            cfg.CreateMap<Source, Destination<TMember>>().ForMember(d => d.Value, o =>
            {
                member(o);
                if (atRuntime)
                {
                    o.MapAtRuntime();
                }
            });
        });
        configuration.AssertConfigurationIsValid();
        configuration.FindTypeMapFor<Source, Destination<TMember>>().PropertyMaps.Single().Inline.ShouldBe(!atRuntime);
        return configuration.CreateMapper();
    }
    // The expected value is the one the inlined member gave before the fix: mapping at runtime must not change it.
    static void ShouldMapTo<TSourceMember, TMember>(Source source, Expression<Func<Source, TSourceMember>> mapFrom, TMember expected)
    {
        foreach (var atRuntime in InlinedAndAtRuntime)
        {
            MapperFor<TMember>(atRuntime, o => o.MapFrom(mapFrom)).Map<Destination<TMember>>(source).Value.ShouldBe(expected, atRuntime ? "mapped at runtime" : "inlined");
        }
    }

    class SourceObject
    {
        public string MyValue { get; set; }
    }
    class DestObject
    {
        public int? MyValue { get; set; }
    }
    [Fact]
    public void The_reported_map()
    {
        var configuration = new MapperConfiguration(cfg => cfg.CreateMap<SourceObject, DestObject>().ForMember(d => d.MyValue, o =>
        {
            o.MapFrom(s => s.MyValue.ToInt());
            o.MapAtRuntime();
        }));
        configuration.AssertConfigurationIsValid();

        configuration.CreateMapper().Map<DestObject>(new SourceObject { MyValue = "1" }).MyValue.ShouldBe(1);
    }

    [Fact]
    public void An_extension_method_call()
    {
        ShouldMapTo<int, int?>(new() { Text = "1" }, s => s.Text.ToInt(), 1);
        ShouldMapTo<Color, Color?>(new() { Text = "Green" }, s => s.Text.ToColor(), Color.Green);
        ShouldMapTo<Point, Point?>(new() { Text = "3" }, s => s.Text.ToPoint(), new Point(3, 0));
    }
    [Fact]
    public void An_extension_method_call_on_a_null_is_not_made()
    {
        ShouldMapTo<int, int?>(new(), s => s.Text.ToInt(), null);
        ShouldMapTo<Color, Color?>(new(), s => s.Text.ToColor(), null);
        ShouldMapTo<Point, Point?>(new(), s => s.Text.ToPoint(), null);
    }
    [Fact]
    public void An_extension_method_call_at_the_end_of_a_chain()
    {
        ShouldMapTo<int, int?>(new() { Inner = new() { Text = "7" } }, s => s.Inner.Text.ToInt(), 7);
        ShouldMapTo<int, int?>(new() { Inner = new() }, s => s.Inner.Text.ToInt(), null);
        ShouldMapTo<int, int?>(new(), s => s.Inner.Text.ToInt(), null);
        ShouldMapTo<int, int?>(new() { Inner = new() { Number = 5 } }, s => s.Inner.Number.Doubled(), 10);
        ShouldMapTo<int, int?>(new(), s => s.Inner.Number.Doubled(), null);
    }
    [Fact]
    public void An_instance_method_call()
    {
        ShouldMapTo<int, int?>(new() { Inner = new() { Number = 5 } }, s => s.Inner.GetNumber(), 5);
        ShouldMapTo<Color, Color?>(new() { Inner = new() { Color = Color.Blue } }, s => s.Inner.GetColor(), Color.Blue);
        ShouldMapTo<Point, Point?>(new() { Inner = new() { Point = new(1, 2) } }, s => s.Inner.GetPoint(), new Point(1, 2));
    }
    [Fact]
    public void An_instance_method_call_on_a_null_is_not_made()
    {
        ShouldMapTo<int, int?>(new(), s => s.Inner.GetNumber(), null);
        ShouldMapTo<Color, Color?>(new(), s => s.Inner.GetColor(), null);
        ShouldMapTo<Point, Point?>(new(), s => s.Inner.GetPoint(), null);
    }
    [Fact]
    public void A_static_method_call()
    {
        ShouldMapTo<int, int?>(new() { Text = "1" }, s => Parse.ToInt(s.Text), 1);
        ShouldMapTo<Color, Color?>(new() { Text = "Green" }, s => Parse.ToColor(s.Text), Color.Green);
        ShouldMapTo<Point, Point?>(new() { Text = "3" }, s => Parse.ToPoint(s.Text), new Point(3, 0));
        ShouldMapTo<int, int?>(new() { Inner = new() { Text = "7" } }, s => Parse.ToInt(s.Inner.Text), 7);
    }
    [Fact]
    public void A_static_method_call_that_throws_on_a_null_gives_the_default_of_what_it_returns()
    {
        ShouldMapTo<int, int?>(new(), s => Parse.ToInt(s.Text), 0);
        ShouldMapTo<Color, Color?>(new(), s => Parse.ToColor(s.Text), Color.Red);
        ShouldMapTo<Point, Point?>(new(), s => Parse.ToPoint(s.Text), new Point());
        ShouldMapTo<int, int?>(new(), s => Parse.ToInt(s.Inner.Text), 0);
    }
    [Fact]
    public void A_member()
    {
        ShouldMapTo<int, int?>(new() { Number = 5 }, s => s.Number, 5);
        ShouldMapTo<Color, Color?>(new() { Color = Color.Blue }, s => s.Color, Color.Blue);
        ShouldMapTo<Point, Point?>(new() { Point = new(1, 2) }, s => s.Point, new Point(1, 2));
    }
    [Fact]
    public void A_member_of_a_member()
    {
        ShouldMapTo<int, int?>(new() { Inner = new() { Number = 5 } }, s => s.Inner.Number, 5);
        ShouldMapTo<Color, Color?>(new() { Inner = new() { Color = Color.Blue } }, s => s.Inner.Color, Color.Blue);
        ShouldMapTo<Point, Point?>(new() { Inner = new() { Point = new(1, 2) } }, s => s.Inner.Point, new Point(1, 2));
        ShouldMapTo<int, int?>(new() { Inner = new() { Innermost = new() { Number = 5 } } }, s => s.Inner.Innermost.Number, 5);
    }
    [Fact]
    public void A_member_of_a_member_that_is_null()
    {
        ShouldMapTo<int, int?>(new(), s => s.Inner.Number, null);
        ShouldMapTo<Color, Color?>(new(), s => s.Inner.Color, null);
        ShouldMapTo<Point, Point?>(new(), s => s.Inner.Point, null);
        ShouldMapTo<int, int?>(new(), s => s.Inner.Innermost.Number, null);
        ShouldMapTo<int, int?>(new() { Inner = new() }, s => s.Inner.Innermost.Number, null);
    }
    [Fact]
    public void A_nullable_into_its_value_type()
    {
        ShouldMapTo<int?, int>(new() { NullableNumber = 5 }, s => s.NullableNumber, 5);
        ShouldMapTo<int?, int>(new(), s => s.NullableNumber, 0);
        ShouldMapTo<int?, int>(new() { Inner = new() { NullableNumber = 5 } }, s => s.Inner.NullableNumber, 5);
        ShouldMapTo<int?, int>(new() { Inner = new() }, s => s.Inner.NullableNumber, 0);
        ShouldMapTo<int?, int>(new(), s => s.Inner.NullableNumber, 0);
        ShouldMapTo<int?, int>(new() { Text = "1" }, s => s.Text.ToNullableInt(), 1);
        ShouldMapTo<int?, int>(new() { Text = "" }, s => s.Text.ToNullableInt(), 0);
        ShouldMapTo<int?, int>(new(), s => s.Text.ToNullableInt(), 0);
    }
    [Fact]
    public void A_nullable_into_a_nullable()
    {
        ShouldMapTo<int?, int?>(new() { Inner = new() { NullableNumber = 5 } }, s => s.Inner.NullableNumber, 5);
        ShouldMapTo<int?, int?>(new() { Inner = new() }, s => s.Inner.NullableNumber, null);
        ShouldMapTo<int?, int?>(new(), s => s.Inner.NullableNumber, null);
        ShouldMapTo<int?, int?>(new() { Text = "1" }, s => s.Text.ToNullableInt(), 1);
        ShouldMapTo<int?, int?>(new(), s => s.Text.ToNullableInt(), null);
    }
    [Fact]
    public void Into_the_nullable_of_another_value_type_a_null_on_the_way_is_the_default_of_the_source_type()
    {
        ShouldMapTo<int, long?>(new() { Inner = new() { Number = 5 } }, s => s.Inner.Number, 5);
        ShouldMapTo<int, long?>(new(), s => s.Inner.Number, 0);
        ShouldMapTo<int, long?>(new() { Text = "1" }, s => s.Text.ToInt(), 1);
        ShouldMapTo<int, long?>(new(), s => s.Text.ToInt(), 0);
    }

    class Flattened
    {
        public int? Number { get; set; }
        public int? InnerNumber { get; set; }
        public Color? InnerColor { get; set; }
        public Point? InnerPoint { get; set; }
        public int? InnerGetNumber { get; set; }
        public int? InnerInnermostNumber { get; set; }
    }
    [Fact]
    public void Members_matched_by_name()
    {
        foreach (var atRuntime in InlinedAndAtRuntime)
        {
            var configuration = new MapperConfiguration(cfg =>
            {
                var map = cfg.CreateMap<Source, Flattened>();
                if (atRuntime)
                {
                    map.ForAllMembers(o => o.MapAtRuntime());
                }
            });
            configuration.AssertConfigurationIsValid();
            configuration.FindTypeMapFor<Source, Flattened>().PropertyMaps.ShouldAllBe(propertyMap => propertyMap.Inline == !atRuntime);
            var mapper = configuration.CreateMapper();

            var destination = mapper.Map<Flattened>(new Source { Number = 1, Inner = new() { Number = 5, Color = Color.Blue, Point = new(1, 2), Innermost = new() { Number = 7 } } });
            destination.Number.ShouldBe(1);
            destination.InnerNumber.ShouldBe(5);
            destination.InnerColor.ShouldBe(Color.Blue);
            destination.InnerPoint.ShouldBe(new Point(1, 2));
            destination.InnerGetNumber.ShouldBe(5);
            destination.InnerInnermostNumber.ShouldBe(7);

            destination = mapper.Map<Flattened>(new Source { Number = 1 });
            destination.Number.ShouldBe(1);
            destination.InnerNumber.ShouldBeNull();
            destination.InnerColor.ShouldBeNull();
            destination.InnerPoint.ShouldBeNull();
            destination.InnerGetNumber.ShouldBeNull();
            destination.InnerInnermostNumber.ShouldBeNull();
        }
    }
    [Fact]
    public void A_path_of_source_members()
    {
        foreach (var atRuntime in InlinedAndAtRuntime)
        {
            var mapper = MapperFor<int?>(atRuntime, o => o.MapFrom("Inner.Number"));

            mapper.Map<Destination<int?>>(new Source { Inner = new() { Number = 5 } }).Value.ShouldBe(5);
            mapper.Map<Destination<int?>>(new Source()).Value.ShouldBeNull();
        }
    }
    [Fact]
    public void A_null_substitute_replaces_the_null_on_the_way()
    {
        foreach (var atRuntime in InlinedAndAtRuntime)
        {
            var mapper = MapperFor<int?>(atRuntime, o =>
            {
                o.MapFrom(s => s.Inner.Text.ToInt());
                o.NullSubstitute(9);
            });

            mapper.Map<Destination<int?>>(new Source { Inner = new() { Text = "5" } }).Value.ShouldBe(5);
            mapper.Map<Destination<int?>>(new Source()).Value.ShouldBe(9);
        }
    }
    [Fact]
    public void Into_an_existing_destination()
    {
        foreach (var atRuntime in InlinedAndAtRuntime)
        {
            var mapper = MapperFor<int?>(atRuntime, o => o.MapFrom(s => s.Inner.Text.ToInt()));

            mapper.Map(new Source { Inner = new() { Text = "5" } }, new Destination<int?> { Value = 3 }).Value.ShouldBe(5);
            mapper.Map(new Source(), new Destination<int?> { Value = 3 }).Value.ShouldBeNull();
        }
    }

    // A map from T to T? made by the user is used for the member whether the member is inlined or not.
    static void MapNumbersToNullable(IMapperConfigurationExpression cfg) => cfg.CreateMap<int, int?>().ConstructUsing(number => number + 100);
    [Fact]
    public void The_map_configured_for_the_types_of_the_member_is_used()
    {
        foreach (var atRuntime in InlinedAndAtRuntime)
        {
            var mapper = MapperFor<int?>(atRuntime, o => o.MapFrom(s => s.Inner.Text.ToInt()), MapNumbersToNullable);

            mapper.Map<Destination<int?>>(new Source { Inner = new() { Text = "5" } }).Value.ShouldBe(105);
            mapper.Map<Destination<int?>>(new Source()).Value.ShouldBeNull();
            mapper.Map(new Source { Inner = new() { Text = "5" } }, new Destination<int?> { Value = 3 }).Value.ShouldBe(105);
            mapper.Map(new Source(), new Destination<int?> { Value = 3 }).Value.ShouldBeNull();
        }
    }

    // Without MapAtRuntime a member is mapped through the context when it has a type map of its own that lies beyond
    // MaxExecutionPlanDepth (or preserves references). Constructor parameters and paths get there only this way.
    class WithConstructor(int? number, int? parsed)
    {
        public int? Number { get; } = number;
        public int? Parsed { get; } = parsed;
    }
    class WithPath
    {
        public Nested Nested { get; set; }
    }
    class Nested
    {
        public int? Number { get; set; }
        public int? Parsed { get; set; }
    }
    static readonly int[] WithinAndBeyondThePlanDepth = [1, 0];
    static IMapper MapperFor(int maxExecutionPlanDepth, Action<IMapperConfigurationExpression> configure, Func<TypeMap, IEnumerable<MemberMap>> memberMaps)
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.Internal().MaxExecutionPlanDepth = maxExecutionPlanDepth;
            MapNumbersToNullable(cfg);
            configure(cfg);
        });
        var members = memberMaps(configuration.GetAllTypeMaps().Single(typeMap => typeMap.SourceType == typeof(Source))).ToArray();
        members.Length.ShouldBe(2);
        members.ShouldAllBe(memberMap => memberMap.Inline == (maxExecutionPlanDepth > 0));
        return configuration.CreateMapper();
    }
    [Fact]
    public void A_member_beyond_the_plan_depth()
    {
        foreach (var maxExecutionPlanDepth in WithinAndBeyondThePlanDepth)
        {
            var mapper = MapperFor(maxExecutionPlanDepth, cfg => cfg.CreateMap<Source, Nested>()
                .ForMember(d => d.Number, o => o.MapFrom(s => s.Inner.Number))
                .ForMember(d => d.Parsed, o => o.MapFrom(s => s.Inner.Text.ToInt())), typeMap => typeMap.PropertyMaps);

            var destination = mapper.Map<Nested>(new Source { Inner = new() { Number = 5, Text = "7" } });
            destination.Number.ShouldBe(105);
            destination.Parsed.ShouldBe(107);
            destination = mapper.Map<Nested>(new Source());
            destination.Number.ShouldBeNull();
            destination.Parsed.ShouldBeNull();
        }
    }
    [Fact]
    public void A_constructor_parameter_beyond_the_plan_depth()
    {
        foreach (var maxExecutionPlanDepth in WithinAndBeyondThePlanDepth)
        {
            var mapper = MapperFor(maxExecutionPlanDepth, cfg => cfg.CreateMap<Source, WithConstructor>()
                .ForCtorParam("number", o => o.MapFrom(s => s.Inner.Number))
                .ForCtorParam("parsed", o => o.MapFrom(s => s.Inner.Text.ToInt())), typeMap => typeMap.ConstructorMap.CtorParams);

            var destination = mapper.Map<WithConstructor>(new Source { Inner = new() { Number = 5, Text = "7" } });
            destination.Number.ShouldBe(105);
            destination.Parsed.ShouldBe(107);
            // A null on the way gives null, inlined or not. Inlined, the map of the parameter's types was invoked
            // without a null check, and the result was "Nullable object must have a value"; that is fixed too
            // (see NullOnTheWayToNullableConverter).
            destination = mapper.Map<WithConstructor>(new Source());
            destination.Number.ShouldBeNull();
            destination.Parsed.ShouldBeNull();
        }
    }
    [Fact]
    public void A_path_beyond_the_plan_depth()
    {
        foreach (var maxExecutionPlanDepth in WithinAndBeyondThePlanDepth)
        {
            var mapper = MapperFor(maxExecutionPlanDepth, cfg => cfg.CreateMap<Source, WithPath>()
                .ForPath(d => d.Nested.Number, o => o.MapFrom(s => s.Inner.Number))
                .ForPath(d => d.Nested.Parsed, o => o.MapFrom(s => s.Inner.Text.ToInt())), typeMap => typeMap.PathMaps);

            var destination = mapper.Map<WithPath>(new Source { Inner = new() { Number = 5, Text = "7" } });
            destination.Nested.Number.ShouldBe(105);
            destination.Nested.Parsed.ShouldBe(107);
            destination = mapper.Map<WithPath>(new Source());
            destination.Nested.Number.ShouldBeNull();
            destination.Nested.Parsed.ShouldBeNull();
        }
    }
}
static class MapAtRuntimeToNullableExtensions
{
    public static int ToInt(this string text) => int.Parse(text);
    public static int? ToNullableInt(this string text) => text.Length == 0 ? null : int.Parse(text);
    public static int Doubled(this int number) => 2 * number;
    public static MapAtRuntimeToNullable.Color ToColor(this string text) => Enum.Parse<MapAtRuntimeToNullable.Color>(text);
    public static MapAtRuntimeToNullable.Point ToPoint(this string text) => new(int.Parse(text), 0);
}
