namespace ForkForward.Mapper.UnitTests.Bug;

// A value that comes from a chain (members, method calls, a member flattened by name) is null checked link by link,
// and when it is a T that goes into a T?, the checked chain is a T?, so that a null on the way maps to null
// (see MapAtRuntimeToNullable). The member map still maps from T, and two things were given the checked value as a T,
// with a cast that throws "Nullable object must have a value" for the null:
// - a map from T to T? made by the user, for a member that is inlined into the plan (the default): always when the map
//   has a type converter, and whatever the map when the member is a constructor parameter or the destination is a value type;
// - a value converter that takes a T, on a member whose source members are matched by name, inlined or not.
// A null on the way now gives what the member gave all along when it was mapped through the context (MapAtRuntime): null,
// and the converter is not called. Every test below runs the member both ways and expects the same.
public class NullOnTheWayToNullableConverter
{
    public enum Color { Red, Green, Blue }
    public readonly record struct Point(int X, int Y);
    public class Source
    {
        public int Number { get; set; }
        public int? NullableNumber { get; set; }
        public Inner Inner { get; set; }
    }
    public class Inner
    {
        public int Number { get; set; }
        public Color Color { get; set; }
        public Point Point { get; set; }
        public Innermost Innermost { get; set; }
        public int GetNumber() => Number;
    }
    public class Innermost
    {
        public int Number { get; set; }
    }
    public class Destination<TMember>
    {
        public TMember Value { get; set; }
    }
    static readonly bool[] InlinedAndAtRuntime = [false, true];
    static string Route(bool atRuntime) => atRuntime ? "mapped at runtime" : "inlined";

    // Every converter adds a hundred: called with a made-up default(int) it would give 100, never null.
    static void AddHundred(IMapperConfigurationExpression cfg) => cfg.CreateMap<int, int?>().ConvertUsing(number => number + 100);
    class AddHundredConverter : ITypeConverter<int, int?>
    {
        public int? Convert(int source, int? destination, ResolutionContext context) => source + 100;
    }

    static IMapper MapperFor<TMember>(bool atRuntime, Action<IMapperConfigurationExpression> configure, Action<IMemberConfigurationExpression<Source, Destination<TMember>, TMember>> member)
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            configure(cfg);
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
    static void ShouldMapTo<TMember>(Action<IMapperConfigurationExpression> configure, Action<IMemberConfigurationExpression<Source, Destination<TMember>, TMember>> member,
        Source source, TMember expected)
    {
        foreach (var atRuntime in InlinedAndAtRuntime)
        {
            MapperFor(atRuntime, configure, member).Map<Destination<TMember>>(source).Value.ShouldBe(expected, Route(atRuntime));
        }
    }

    [Fact]
    public void The_reported_map()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<int, int?>().ConvertUsing(i => i * 2);
            cfg.CreateMap<Source, Destination<int?>>().ForMember(d => d.Value, o => o.MapFrom(s => s.Inner.Number));
        });
        configuration.AssertConfigurationIsValid();
        var mapper = configuration.CreateMapper();

        mapper.Map<Destination<int?>>(new Source { Inner = new() { Number = 5 } }).Value.ShouldBe(10);
        mapper.Map<Destination<int?>>(new Source()).Value.ShouldBeNull();
    }
    [Fact]
    public void A_type_converter_in_every_form()
    {
        Action<IMappingExpression<int, int?>>[] forms =
        [
            map => map.ConvertUsing(number => number + 100),
            map => map.ConvertUsing((number, _) => number + 100),
            map => map.ConvertUsing((number, _, _) => number + 100),
            map => map.ConvertUsing(new AddHundredConverter()),
            map => map.ConvertUsing<AddHundredConverter>(),
            map => map.ConvertUsing(typeof(AddHundredConverter)),
        ];
        foreach (var form in forms)
        {
            ShouldMapTo<int?>(cfg => form(cfg.CreateMap<int, int?>()), o => o.MapFrom(s => s.Inner.Number), new() { Inner = new() { Number = 5 } }, 105);
            ShouldMapTo<int?>(cfg => form(cfg.CreateMap<int, int?>()), o => o.MapFrom(s => s.Inner.Number), new(), null);
        }
    }
    [Fact]
    public void The_type_converter_is_not_called_for_a_null_on_the_way()
    {
        foreach (var atRuntime in InlinedAndAtRuntime)
        {
            var calls = 0;
            var mapper = MapperFor<int?>(atRuntime, cfg => cfg.CreateMap<int, int?>().ConvertUsing((number, _) =>
            {
                calls++;
                return number + 100;
            }), o => o.MapFrom(s => s.Inner.Number));

            mapper.Map<Destination<int?>>(new Source()).Value.ShouldBeNull(Route(atRuntime));
            calls.ShouldBe(0, Route(atRuntime));
            mapper.Map<Destination<int?>>(new Source { Inner = new() }).Value.ShouldBe(100, Route(atRuntime));
            calls.ShouldBe(1, Route(atRuntime));
        }
    }
    [Fact]
    public void An_enum_and_a_struct()
    {
        static void Maps(IMapperConfigurationExpression cfg)
        {
            cfg.CreateMap<Color, Color?>().ConvertUsing(color => color + 1);
            cfg.CreateMap<Point, Point?>().ConvertUsing(point => new Point(point.X + 100, point.Y));
        }
        ShouldMapTo<Color?>(Maps, o => o.MapFrom(s => s.Inner.Color), new() { Inner = new() { Color = Color.Green } }, Color.Blue);
        ShouldMapTo<Color?>(Maps, o => o.MapFrom(s => s.Inner.Color), new(), null);
        ShouldMapTo<Point?>(Maps, o => o.MapFrom(s => s.Inner.Point), new() { Inner = new() { Point = new(1, 2) } }, new Point(101, 2));
        ShouldMapTo<Point?>(Maps, o => o.MapFrom(s => s.Inner.Point), new(), null);
    }
    [Fact]
    public void Longer_chains_method_calls_and_a_path_of_source_members()
    {
        ShouldMapTo<int?>(AddHundred, o => o.MapFrom(s => s.Inner.Innermost.Number), new() { Inner = new() { Innermost = new() { Number = 5 } } }, 105);
        ShouldMapTo<int?>(AddHundred, o => o.MapFrom(s => s.Inner.Innermost.Number), new() { Inner = new() }, null);
        ShouldMapTo<int?>(AddHundred, o => o.MapFrom(s => s.Inner.Innermost.Number), new(), null);
        ShouldMapTo<int?>(AddHundred, o => o.MapFrom(s => s.Inner.GetNumber()), new() { Inner = new() { Number = 5 } }, 105);
        ShouldMapTo<int?>(AddHundred, o => o.MapFrom(s => s.Inner.GetNumber()), new(), null);
        ShouldMapTo<int?>(AddHundred, o => o.MapFrom("Inner.Number"), new() { Inner = new() { Number = 5 } }, 105);
        ShouldMapTo<int?>(AddHundred, o => o.MapFrom("Inner.Number"), new(), null);
    }

    class Flattened
    {
        public int? Number { get; set; }
        public int? InnerNumber { get; set; }
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
                AddHundred(cfg);
                var map = cfg.CreateMap<Source, Flattened>();
                if (atRuntime)
                {
                    map.ForAllMembers(o => o.MapAtRuntime());
                }
            });
            configuration.AssertConfigurationIsValid();
            configuration.FindTypeMapFor<Source, Flattened>().PropertyMaps.ShouldAllBe(propertyMap => propertyMap.Inline == !atRuntime);
            var mapper = configuration.CreateMapper();

            var destination = mapper.Map<Flattened>(new Source { Number = 1, Inner = new() { Number = 5, Innermost = new() { Number = 7 } } });
            destination.Number.ShouldBe(101, Route(atRuntime));
            destination.InnerNumber.ShouldBe(105, Route(atRuntime));
            destination.InnerGetNumber.ShouldBe(105, Route(atRuntime));
            destination.InnerInnermostNumber.ShouldBe(107, Route(atRuntime));

            destination = mapper.Map<Flattened>(new Source { Number = 1 });
            destination.Number.ShouldBe(101, Route(atRuntime));
            destination.InnerNumber.ShouldBeNull(Route(atRuntime));
            destination.InnerGetNumber.ShouldBeNull(Route(atRuntime));
            destination.InnerInnermostNumber.ShouldBeNull(Route(atRuntime));
        }
    }
    [Fact]
    public void A_null_substitute_replaces_the_null_on_the_way_and_is_converted()
    {
        Action<IMemberConfigurationExpression<Source, Destination<int?>, int?>> member = o =>
        {
            o.MapFrom(s => s.Inner.Number);
            o.NullSubstitute(9);
        };
        ShouldMapTo(AddHundred, member, new() { Inner = new() { Number = 5 } }, 105);
        ShouldMapTo(AddHundred, member, new(), 109);
    }
    [Fact]
    public void Into_an_existing_destination()
    {
        foreach (var atRuntime in InlinedAndAtRuntime)
        {
            var mapper = MapperFor<int?>(atRuntime, AddHundred, o => o.MapFrom(s => s.Inner.Number));

            mapper.Map(new Source { Inner = new() { Number = 5 } }, new Destination<int?> { Value = 3 }).Value.ShouldBe(105, Route(atRuntime));
            mapper.Map(new Source(), new Destination<int?> { Value = 3 }).Value.ShouldBeNull(Route(atRuntime));
        }
    }
    [Fact]
    public void With_UseDestinationValue_a_null_on_the_way_leaves_the_destination_value()
    {
        foreach (var atRuntime in InlinedAndAtRuntime)
        {
            var mapper = MapperFor<int?>(atRuntime, AddHundred, o =>
            {
                o.MapFrom(s => s.Inner.Number);
                o.UseDestinationValue();
            });

            mapper.Map(new Source { Inner = new() { Number = 5 } }, new Destination<int?> { Value = 3 }).Value.ShouldBe(105, Route(atRuntime));
            mapper.Map(new Source(), new Destination<int?> { Value = 3 }).Value.ShouldBe(3, Route(atRuntime));
        }
    }
    [Fact]
    public void When_null_destination_values_are_not_allowed()
    {
        static void Maps(IMapperConfigurationExpression cfg)
        {
            cfg.AllowNullDestinationValues = false;
            AddHundred(cfg);
        }
        ShouldMapTo<int?>(Maps, o => o.MapFrom(s => s.Inner.Number), new() { Inner = new() { Number = 5 } }, 105);
        ShouldMapTo<int?>(Maps, o => o.MapFrom(s => s.Inner.Number), new(), null);
    }

    // A constructor parameter and a member of a destination that is a value type are mapped into a default value,
    // and a map without a type converter is then invoked without a null check as well.
    class WithConstructor(int? number, int? flattenedInnerNumber = 7)
    {
        public int? Number { get; } = number;
        public int? FlattenedInnerNumber { get; } = flattenedInnerNumber;
    }
    public static IEnumerable<object[]> MapsFromNumberToNullable() =>
    [
        [(Action<IMapperConfigurationExpression>)(cfg => cfg.CreateMap<int, int?>().ConvertUsing(number => number + 100)), 105],
        [(Action<IMapperConfigurationExpression>)(cfg => cfg.CreateMap<int, int?>().ConstructUsing(number => number + 100)), 105],
        [(Action<IMapperConfigurationExpression>)(cfg => cfg.CreateMap<int, int?>().ConstructUsing((number, _) => number + 100)), 105],
        // A map from int to int? without any configuration creates a default int? and has no members to set: it maps to null.
        [(Action<IMapperConfigurationExpression>)(cfg => cfg.CreateMap<int, int?>()), null],
    ];
    [Theory, MemberData(nameof(MapsFromNumberToNullable))]
    public void A_constructor_parameter(Action<IMapperConfigurationExpression> mapNumbers, int? mapped)
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            mapNumbers(cfg);
            cfg.CreateMap<Source, WithConstructor>()
                .ForCtorParam("number", o => o.MapFrom(s => s.Inner.Number))
                .ForCtorParam("flattenedInnerNumber", o => o.MapFrom("Inner.Number"));
        });
        configuration.AssertConfigurationIsValid();
        configuration.FindTypeMapFor<Source, WithConstructor>().ConstructorMap.CtorParams.ShouldAllBe(parameter => parameter.Inline);
        var mapper = configuration.CreateMapper();

        var destination = mapper.Map<WithConstructor>(new Source { Inner = new() { Number = 5 } });
        destination.Number.ShouldBe(mapped);
        destination.FlattenedInnerNumber.ShouldBe(mapped);
        destination = mapper.Map<WithConstructor>(new Source());
        destination.Number.ShouldBeNull();
        destination.FlattenedInnerNumber.ShouldBeNull();
    }
    struct ValueDestination
    {
        public int? Value { get; set; }
    }
    [Theory, MemberData(nameof(MapsFromNumberToNullable))]
    public void A_member_of_a_destination_that_is_a_value_type(Action<IMapperConfigurationExpression> mapNumbers, int? mapped)
    {
        foreach (var atRuntime in InlinedAndAtRuntime)
        {
            var configuration = new MapperConfiguration(cfg =>
            {
                mapNumbers(cfg);
                cfg.CreateMap<Source, ValueDestination>().ForMember(d => d.Value, o =>
                {
                    o.MapFrom(s => s.Inner.Number);
                    if (atRuntime)
                    {
                        o.MapAtRuntime();
                    }
                });
            });
            configuration.AssertConfigurationIsValid();
            var mapper = configuration.CreateMapper();

            mapper.Map<ValueDestination>(new Source { Inner = new() { Number = 5 } }).Value.ShouldBe(mapped, Route(atRuntime));
            mapper.Map<ValueDestination>(new Source()).Value.ShouldBeNull(Route(atRuntime));
        }
    }
    class WithPath
    {
        public Destination<int?> Nested { get; set; }
    }
    [Fact]
    public void A_path_in_the_destination()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            AddHundred(cfg);
            cfg.CreateMap<Source, WithPath>().ForPath(d => d.Nested.Value, o => o.MapFrom(s => s.Inner.Number));
        });
        configuration.AssertConfigurationIsValid();
        var mapper = configuration.CreateMapper();

        mapper.Map<WithPath>(new Source { Inner = new() { Number = 5 } }).Nested.Value.ShouldBe(105);
        mapper.Map<WithPath>(new Source()).Nested.Value.ShouldBeNull();
    }

    // What worked is not changed: the three facts below give what they gave before.
    [Fact]
    public void A_member_that_is_not_a_chain_is_converted_as_before()
    {
        ShouldMapTo<int?>(AddHundred, o => o.MapFrom(s => s.Number), new() { Number = 5 }, 105);
        ShouldMapTo<int?>(AddHundred, o => o.MapFrom(s => s.Number), new(), 100);
    }
    [Fact]
    public void Into_a_member_that_is_not_nullable_the_converter_is_given_the_default_as_before()
    {
        static void Maps(IMapperConfigurationExpression cfg) => cfg.CreateMap<int, int>().ConvertUsing(number => number + 100);
        ShouldMapTo<int>(Maps, o => o.MapFrom(s => s.Inner.Number), new() { Inner = new() { Number = 5 } }, 105);
        ShouldMapTo<int>(Maps, o => o.MapFrom(s => s.Inner.Number), new(), 100);
    }
    [Fact]
    public void An_inlined_map_from_a_reference_type_is_given_the_null_as_before()
    {
        // The map that is found for int -> int? takes object, and a null is a source it can be given. Inlined, its type
        // converter gets the null on the way, as it did in AutoMapper 14. (Mapped at runtime, the member gave null and
        // the converter was not called; that difference between the two routes is older than this fix and is not changed.)
        var mapper = MapperFor<int?>(atRuntime: false, cfg => cfg.CreateMap<object, int?>().ConvertUsing(value => value == null ? 42 : (int)value + 100),
            o => o.MapFrom(s => s.Inner.Number));

        mapper.Map<Destination<int?>>(new Source { Inner = new() { Number = 5 } }).Value.ShouldBe(105);
        mapper.Map<Destination<int?>>(new Source()).Value.ShouldBe(42);
    }

    // Value converters.
    class FlattenedNumber
    {
        public int? InnerNumber { get; set; }
    }
    class AddHundredValueConverter : IValueConverter<int, int?>
    {
        public int Calls;
        public int? Convert(int sourceMember, ResolutionContext context)
        {
            Calls++;
            return sourceMember + 100;
        }
    }
    class NumberToText : IValueConverter<int, string>
    {
        public string Convert(int sourceMember, ResolutionContext context) => (sourceMember + 100).ToString();
    }
    class NumberToLong : IValueConverter<int, long>
    {
        public long Convert(int sourceMember, ResolutionContext context) => sourceMember + 100L;
    }
    class NullableNumberConverter : IValueConverter<int?, int?>
    {
        public int? Convert(int? sourceMember, ResolutionContext context) => sourceMember == null ? -1 : sourceMember + 100;
    }
    class ObjectConverter : IValueConverter<object, int?>
    {
        public int? Convert(object sourceMember, ResolutionContext context) => sourceMember == null ? -1 : (int)sourceMember + 100;
    }
    static IMapper FlattenedMapper(bool atRuntime, Action<IMemberConfigurationExpression<Source, FlattenedNumber, int?>> member)
    {
        var configuration = new MapperConfiguration(cfg => cfg.CreateMap<Source, FlattenedNumber>().ForMember(d => d.InnerNumber, o =>
        {
            member(o);
            if (atRuntime)
            {
                o.MapAtRuntime();
            }
        }));
        configuration.AssertConfigurationIsValid();
        configuration.FindTypeMapFor<Source, FlattenedNumber>().PropertyMaps.Single().Inline.ShouldBe(!atRuntime);
        return configuration.CreateMapper();
    }
    static IMapper FlattenedMapper(bool atRuntime, Action<IMemberConfigurationExpression> member)
    {
        var configuration = new MapperConfiguration(cfg => cfg.CreateMap(typeof(Source), typeof(FlattenedNumber)).ForMember(nameof(FlattenedNumber.InnerNumber), o =>
        {
            member(o);
            if (atRuntime)
            {
                o.MapAtRuntime();
            }
        }));
        configuration.AssertConfigurationIsValid();
        return configuration.CreateMapper();
    }
    [Fact]
    public void The_reported_value_converter()
    {
        var configuration = new MapperConfiguration(cfg => cfg.CreateMap<Source, FlattenedNumber>().ForMember(d => d.InnerNumber, o => o.ConvertUsing(new AddHundredValueConverter())));
        configuration.AssertConfigurationIsValid();
        var mapper = configuration.CreateMapper();

        mapper.Map<FlattenedNumber>(new Source { Inner = new() { Number = 5 } }).InnerNumber.ShouldBe(105);
        mapper.Map<FlattenedNumber>(new Source()).InnerNumber.ShouldBeNull();
    }
    [Fact]
    public void A_value_converter_in_every_form()
    {
        foreach (var atRuntime in InlinedAndAtRuntime)
        {
            IMapper[] mappers =
            [
                FlattenedMapper(atRuntime, o => o.ConvertUsing(new AddHundredValueConverter())),
                FlattenedMapper(atRuntime, o => o.ConvertUsing<AddHundredValueConverter, int>()),
                FlattenedMapper(atRuntime, (IMemberConfigurationExpression o) => o.ConvertUsing(typeof(AddHundredValueConverter))),
            ];
            foreach (var mapper in mappers)
            {
                mapper.Map<FlattenedNumber>(new Source { Inner = new() { Number = 5 } }).InnerNumber.ShouldBe(105, Route(atRuntime));
                mapper.Map<FlattenedNumber>(new Source()).InnerNumber.ShouldBeNull(Route(atRuntime));
            }
        }
    }
    [Fact]
    public void The_value_converter_is_not_called_for_a_null_on_the_way()
    {
        foreach (var atRuntime in InlinedAndAtRuntime)
        {
            var converter = new AddHundredValueConverter();
            var mapper = FlattenedMapper(atRuntime, o => o.ConvertUsing(converter));

            mapper.Map<FlattenedNumber>(new Source()).InnerNumber.ShouldBeNull(Route(atRuntime));
            converter.Calls.ShouldBe(0, Route(atRuntime));
            mapper.Map<FlattenedNumber>(new Source { Inner = new() }).InnerNumber.ShouldBe(100, Route(atRuntime));
            converter.Calls.ShouldBe(1, Route(atRuntime));
        }
    }
    [Fact]
    public void A_value_converter_that_returns_a_reference_type()
    {
        foreach (var atRuntime in InlinedAndAtRuntime)
        {
            var mapper = FlattenedMapper(atRuntime, (IMemberConfigurationExpression o) => o.ConvertUsing(typeof(NumberToText)));

            mapper.Map<FlattenedNumber>(new Source { Inner = new() { Number = 5 } }).InnerNumber.ShouldBe(105, Route(atRuntime));
            mapper.Map<FlattenedNumber>(new Source()).InnerNumber.ShouldBeNull(Route(atRuntime));
        }
    }
    [Fact]
    public void A_value_converter_that_returns_another_value_type_gives_the_default_of_that_type()
    {
        // The converter is not called, and what it returns has no null: as for a chain that ends in an int and goes into
        // a long? (MapAtRuntimeToNullable), the member gets the default of the type that is mapped into it.
        foreach (var atRuntime in InlinedAndAtRuntime)
        {
            var mapper = FlattenedMapper(atRuntime, (IMemberConfigurationExpression o) => o.ConvertUsing(typeof(NumberToLong)));

            mapper.Map<FlattenedNumber>(new Source { Inner = new() { Number = 5 } }).InnerNumber.ShouldBe(105, Route(atRuntime));
            mapper.Map<FlattenedNumber>(new Source()).InnerNumber.ShouldBe(0, Route(atRuntime));
        }
    }
    [Fact]
    public void A_null_substitute_replaces_what_the_value_converter_was_not_called_for()
    {
        // A null substitute replaces a null that the value converter returns; it is not given to the converter.
        foreach (var atRuntime in InlinedAndAtRuntime)
        {
            var mapper = FlattenedMapper(atRuntime, o =>
            {
                o.ConvertUsing(new AddHundredValueConverter());
                o.NullSubstitute(9);
            });

            mapper.Map<FlattenedNumber>(new Source { Inner = new() { Number = 5 } }).InnerNumber.ShouldBe(105, Route(atRuntime));
            mapper.Map<FlattenedNumber>(new Source()).InnerNumber.ShouldBe(9, Route(atRuntime));
        }
    }
    [Fact]
    public void A_value_converter_that_can_take_a_null_is_given_it_as_before()
    {
        foreach (var atRuntime in InlinedAndAtRuntime)
        {
            IMapper[] mappers = [FlattenedMapper(atRuntime, o => o.ConvertUsing(new NullableNumberConverter())), FlattenedMapper(atRuntime, o => o.ConvertUsing(new ObjectConverter()))];
            foreach (var mapper in mappers)
            {
                mapper.Map<FlattenedNumber>(new Source { Inner = new() { Number = 5 } }).InnerNumber.ShouldBe(105, Route(atRuntime));
                mapper.Map<FlattenedNumber>(new Source()).InnerNumber.ShouldBe(-1, Route(atRuntime));
            }
        }
    }
    [Fact]
    public void A_value_converter_on_a_member_that_is_not_a_chain_is_called_as_before()
    {
        foreach (var atRuntime in InlinedAndAtRuntime)
        {
            var mapper = MapperFor<int?>(atRuntime, _ => { }, o => o.ConvertUsing(new AddHundredValueConverter(), s => s.Number));
            mapper.Map<Destination<int?>>(new Source { Number = 5 }).Value.ShouldBe(105, Route(atRuntime));
            mapper.Map<Destination<int?>>(new Source()).Value.ShouldBe(100, Route(atRuntime));

            mapper = MapperFor<int?>(atRuntime, _ => { }, o => o.ConvertUsing(new AddHundredValueConverter(), nameof(Source.Number)));
            mapper.Map<Destination<int?>>(new Source { Number = 5 }).Value.ShouldBe(105, Route(atRuntime));
        }
    }
    [Fact]
    public void A_source_member_given_to_a_value_converter_as_a_lambda_is_not_null_checked_as_before()
    {
        // The lambda is used as it is written, as in AutoMapper 14, also for a member value resolver: a null on the way
        // is a NullReferenceException, whatever the type of the destination member. Not changed by this fix.
        foreach (var atRuntime in InlinedAndAtRuntime)
        {
            var mapper = MapperFor<int?>(atRuntime, _ => { }, o => o.ConvertUsing(new AddHundredValueConverter(), s => s.Inner.Number));

            mapper.Map<Destination<int?>>(new Source { Inner = new() { Number = 5 } }).Value.ShouldBe(105, Route(atRuntime));
            Should.Throw<Exception>(() => mapper.Map<Destination<int?>>(new Source())).GetBaseException().ShouldBeOfType<NullReferenceException>();
        }
    }
}
