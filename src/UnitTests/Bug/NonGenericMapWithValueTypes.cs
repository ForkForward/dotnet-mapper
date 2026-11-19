namespace ForkForward.Mapper.UnitTests.Bug;

// A map made with the non-generic CreateMap(typeof(Source), typeof(Destination)) is configured with lambdas that take
// object. The plan has the source and the destination with their own types and put them into those lambdas as they
// were. For a class that is a valid expression. For a value type it is not, and the configuration could not be created:
// "Expression of type 'Source' cannot be used for parameter of type 'System.Object'", for BeforeMap, AfterMap,
// ConstructUsing, ConvertUsing<TTypeConverter>() and for every expression that passes the source on or casts it
// (MapFrom(source => Read(source)), MapFrom(source => ((Source)source).Value)).
// The same happened where such a lambda reaches a value type by another way: ForAllMaps, an open generic map closed
// with a struct, and a map of a struct that inherits from the map of an interface the struct implements.
// A destination that is a value type could not be given what ConstructUsing or ConstructUsingServiceLocator create:
// "Expression of type 'System.Object' cannot be used for assignment to type 'Destination'".
// The value is now boxed where the lambda needs a reference, and the created object is unboxed.
public class NonGenericMapWithValueTypes
{
    public interface ISource
    {
        int Value { get; }
    }
    public interface IDestination
    {
        int Value { get; }
        int Other { get; set; }
    }
    public struct StructSource : ISource
    {
        public int Value { get; set; }
    }
    public class ClassSource : ISource
    {
        public int Value { get; set; }
    }
    public struct StructDestination : IDestination
    {
        public int Value { get; set; }
        public int Other { get; set; }
    }
    public class ClassDestination : IDestination
    {
        public int Value { get; set; }
        public int Other { get; set; }
    }
    public static IEnumerable<object[]> ValueTypes() =>
    [
        [typeof(StructSource), typeof(ClassDestination)],
        [typeof(ClassSource), typeof(StructDestination)],
        [typeof(StructSource), typeof(StructDestination)],
    ];
    static object Source(Type sourceType, int value) => sourceType == typeof(StructSource) ? new StructSource { Value = value } : new ClassSource { Value = value };
    static int Read(object source) => ((ISource)source).Value;
    static object AddHundred(object value) => (int)value + 100;
    // A destination that remembers the source it was created for.
    static object Create(Type destinationType, object source)
    {
        var destination = (IDestination)Activator.CreateInstance(destinationType);
        destination.Other = Read(source) + 100;
        return destination;
    }
    static IMapper MapperFor(Type sourceType, Type destinationType, Action<IMappingExpression> configure, Func<Type, object> serviceCtor = null)
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            var map = cfg.CreateMap(sourceType, destinationType);
            map.ForMember(nameof(IDestination.Other), o => o.Ignore());
            configure(map);
        });
        configuration.AssertConfigurationIsValid();
        return serviceCtor == null ? configuration.CreateMapper() : configuration.CreateMapper(serviceCtor);
    }
    static IDestination Map(IMapper mapper, Type sourceType, Type destinationType, int value = 3)
    {
        var destination = mapper.Map(Source(sourceType, value), sourceType, destinationType);
        destination.ShouldBeOfType(destinationType);
        return (IDestination)destination;
    }

    public record Call(Type SourceType, int SourceValue, Type DestinationType, int DestinationValue);
    static Call Describe(object source, object destination) => new(source.GetType(), Read(source), destination.GetType(), ((IDestination)destination).Value);
    public class RecordingAction(List<Call> calls) : IMappingAction<object, object>
    {
        public void Process(object source, object destination, ResolutionContext context) => calls.Add(Describe(source, destination));
    }
    [Theory, MemberData(nameof(ValueTypes))]
    public void BeforeMap_and_AfterMap_in_every_form(Type sourceType, Type destinationType)
    {
        (bool Before, Action<IMappingExpression, List<Call>> Configure)[] forms =
        [
            (true, (map, calls) => map.BeforeMap((source, destination) => calls.Add(Describe(source, destination)))),
            (true, (map, calls) => map.BeforeMap((source, destination, context) => calls.Add(Describe(source, destination)))),
            (true, (map, calls) => map.BeforeMap<RecordingAction>()),
            (false, (map, calls) => map.AfterMap((source, destination) => calls.Add(Describe(source, destination)))),
            (false, (map, calls) => map.AfterMap((source, destination, context) => calls.Add(Describe(source, destination)))),
            (false, (map, calls) => map.AfterMap<RecordingAction>()),
        ];
        foreach (var (before, configure) in forms)
        {
            List<Call> calls = [];
            var mapper = MapperFor(sourceType, destinationType, map => configure(map, calls), _ => new RecordingAction(calls));

            Map(mapper, sourceType, destinationType).Value.ShouldBe(3);

            // The action gets the source and the destination with their own types; before the members are mapped or after.
            calls.ShouldBe(new[] { new Call(sourceType, 3, destinationType, before ? 0 : 3) });
        }
    }
    [Fact]
    public void An_action_changes_a_destination_that_is_a_class()
    {
        var mapper = MapperFor(typeof(StructSource), typeof(ClassDestination), map => map
            .BeforeMap((source, destination) => ((ClassDestination)destination).Other = 10 * Read(source))
            .AfterMap((source, destination) => ((ClassDestination)destination).Other++));

        Map(mapper, typeof(StructSource), typeof(ClassDestination)).Other.ShouldBe(31);
    }
    [Fact]
    public void An_action_gets_a_copy_of_a_destination_that_is_a_value_type_as_in_the_generic_API()
    {
        var mapper = MapperFor(typeof(ClassSource), typeof(StructDestination), map => map
            .BeforeMap((source, destination) => ((IDestination)destination).Other = 9)
            .AfterMap((source, destination) => ((IDestination)destination).Other = 9));
        Map(mapper, typeof(ClassSource), typeof(StructDestination)).Other.ShouldBe(0);

        var generic = new MapperConfiguration(cfg => cfg.CreateMap<ClassSource, StructDestination>()
            .ForMember(d => d.Other, o => o.Ignore())
            .BeforeMap((source, destination) => destination.Other = 9)
            .AfterMap((source, destination) => destination.Other = 9));
        generic.CreateMapper().Map<StructDestination>(new ClassSource { Value = 3 }).Other.ShouldBe(0);
    }

    public class ObjectFactory(Type destinationType) : IDestinationFactory<object, object>
    {
        public object Create(object source, ResolutionContext context) => NonGenericMapWithValueTypes.Create(destinationType, source);
    }
    public class TypedFactory<TDestination> : IDestinationFactory<object, TDestination> where TDestination : IDestination, new()
    {
        public TDestination Create(object source, ResolutionContext context)
        {
            var destination = new TDestination();
            destination.Other = Read(source) + 100;
            return destination;
        }
    }
    [Theory, MemberData(nameof(ValueTypes))]
    public void ConstructUsing_in_every_form(Type sourceType, Type destinationType)
    {
        Action<IMappingExpression>[] forms =
        [
            map => map.ConstructUsing(source => Create(destinationType, source)),
            map => map.ConstructUsing((source, context) => Create(destinationType, source)),
            // The two class-based forms worked before: the function they are called through is built for the types of the map.
            map => map.ConstructUsing<ObjectFactory>(),
            map => map.ConstructUsing(typeof(TypedFactory<>).MakeGenericType(destinationType)),
        ];
        foreach (var form in forms)
        {
            var mapper = MapperFor(sourceType, destinationType, form, type => type == typeof(ObjectFactory) ? new ObjectFactory(destinationType) : Activator.CreateInstance(type));

            var destination = Map(mapper, sourceType, destinationType);

            destination.Value.ShouldBe(3);
            destination.Other.ShouldBe(103);
        }
    }
    [Fact]
    public void ConstructUsing_with_an_expression_that_does_not_use_the_source()
    {
        var mapper = MapperFor(typeof(ClassSource), typeof(StructDestination), map => map.ConstructUsing(source => new StructDestination { Other = 7 }));
        var destination = Map(mapper, typeof(ClassSource), typeof(StructDestination));
        destination.Value.ShouldBe(3);
        destination.Other.ShouldBe(7);

        // A source that is a value type worked before in this form, and gives what it gave.
        mapper = MapperFor(typeof(StructSource), typeof(ClassDestination), map => map.ConstructUsing(source => new ClassDestination { Other = 7 }));
        destination = Map(mapper, typeof(StructSource), typeof(ClassDestination));
        destination.Value.ShouldBe(3);
        destination.Other.ShouldBe(7);
    }
    static object CreateWithOther(Type type)
    {
        var destination = (IDestination)Activator.CreateInstance(type);
        destination.Other = 50;
        return destination;
    }
    [Fact]
    public void ConstructUsingServiceLocator_with_a_destination_that_is_a_value_type()
    {
        foreach (var sourceType in new[] { typeof(ClassSource), typeof(StructSource) })
        {
            var mapper = MapperFor(sourceType, typeof(StructDestination), map => map.ConstructUsingServiceLocator(), CreateWithOther);

            var destination = Map(mapper, sourceType, typeof(StructDestination));

            destination.Value.ShouldBe(3);
            destination.Other.ShouldBe(50);
        }
        // The generic API failed in the same way.
        var generic = new MapperConfiguration(cfg => cfg.CreateMap<ClassSource, StructDestination>().ForMember(d => d.Other, o => o.Ignore()).ConstructUsingServiceLocator());
        generic.AssertConfigurationIsValid();
        var mapped = generic.CreateMapper(CreateWithOther).Map<StructDestination>(new ClassSource { Value = 3 });
        mapped.Value.ShouldBe(3);
        mapped.Other.ShouldBe(50);
    }

    public class ObjectConverter(Type destinationType) : ITypeConverter<object, object>
    {
        public object Convert(object source, object destination, ResolutionContext context) => Create(destinationType, source);
    }
    public class TypedConverter<TSource, TDestination> : ITypeConverter<TSource, TDestination> where TDestination : IDestination, new()
    {
        public TDestination Convert(TSource source, TDestination destination, ResolutionContext context)
        {
            var converted = new TDestination();
            converted.Other = Read(source) + 100;
            return converted;
        }
    }
    [Theory, MemberData(nameof(ValueTypes))]
    public void ConvertUsing_in_every_form(Type sourceType, Type destinationType)
    {
        Action<IMappingExpression>[] forms =
        [
            // Only this form failed: the converter class is called through ITypeConverter<object, object>.
            map => map.ConvertUsing<ObjectConverter>(),
            map => map.ConvertUsing(source => Create(destinationType, source)),
            map => map.ConvertUsing((source, destination) => Create(destinationType, source)),
            map => map.ConvertUsing((source, destination, context) => Create(destinationType, source)),
            map => map.ConvertUsing(new ObjectConverter(destinationType)),
            map => map.ConvertUsing(typeof(TypedConverter<,>).MakeGenericType(sourceType, destinationType)),
        ];
        foreach (var form in forms)
        {
            var mapper = MapperFor(sourceType, destinationType, form, type => type == typeof(ObjectConverter) ? new ObjectConverter(destinationType) : Activator.CreateInstance(type));

            Map(mapper, sourceType, destinationType).Other.ShouldBe(103);
        }
    }
    public class DestinationRecorder(List<object> destinations) : ITypeConverter<object, object>
    {
        public object Convert(object source, object destination, ResolutionContext context)
        {
            destinations.Add(destination);
            return new StructDestination();
        }
    }
    [Fact]
    public void A_converter_class_is_given_the_destination_that_a_converter_function_is_given()
    {
        // A destination that is a value type is never null: without one to map into, it is the default value.
        List<object> destinations = [];
        var mapper = MapperFor(typeof(StructSource), typeof(StructDestination), map => map.ConvertUsing<DestinationRecorder>(), _ => new DestinationRecorder(destinations));
        Map(mapper, typeof(StructSource), typeof(StructDestination));
        mapper = MapperFor(typeof(StructSource), typeof(StructDestination), map => map.ConvertUsing((source, destination) =>
        {
            destinations.Add(destination);
            return new StructDestination();
        }));
        Map(mapper, typeof(StructSource), typeof(StructDestination));

        destinations.ShouldBe(new object[] { new StructDestination(), new StructDestination() });
    }

    public class AddHundredResolver : IValueResolver<object, object, object>
    {
        public object Resolve(object source, object destination, object destMember, ResolutionContext context) => Read(source) + 100;
    }
    public class AddHundredMemberResolver : IMemberValueResolver<object, object, int, object>
    {
        public object Resolve(object source, object destination, int sourceMember, object destMember, ResolutionContext context) => sourceMember + 100;
    }
    public class AddHundredValueConverter : IValueConverter<int, object>
    {
        public object Convert(int sourceMember, ResolutionContext context) => sourceMember + 100;
    }
    [Theory, MemberData(nameof(ValueTypes))]
    public void The_source_of_a_member_in_every_form(Type sourceType, Type destinationType)
    {
        Action<IMemberConfigurationExpression>[] forms =
        [
            // These failed with a source that is a value type: an expression that passes the source on or casts it.
            o => o.MapFrom(source => Read(source) + 100),
            o => o.MapFrom(source => ((ISource)source).Value + 100),
            o => o.MapFrom<AddHundredMemberResolver, int>(source => Read(source)),
            o => o.MapFrom(new AddHundredMemberResolver(), source => Read(source)),
            o => o.ConvertUsing(new AddHundredValueConverter(), source => Read(source)),
            o => o.ConvertUsing<AddHundredValueConverter, int>(source => Read(source)),
            // These worked before.
            o => o.MapFrom((source, destination) => Read(source) + 100),
            o => o.MapFrom((source, destination, member) => Read(source) + 100),
            o => o.MapFrom((source, destination, member, context) => Read(source) + 100),
            o => o.MapFrom(typeof(AddHundredResolver)),
            o => o.MapFrom<AddHundredResolver>(),
            o => o.MapFrom(new AddHundredResolver()),
            o => o.MapFrom(typeof(AddHundredMemberResolver), nameof(ISource.Value)),
            o => o.MapFrom<AddHundredMemberResolver, int>(nameof(ISource.Value)),
            o => o.MapFrom(new AddHundredMemberResolver(), nameof(ISource.Value)),
            o => o.ConvertUsing(typeof(AddHundredValueConverter), nameof(ISource.Value)),
            o => o.ConvertUsing(new AddHundredValueConverter(), nameof(ISource.Value)),
        ];
        foreach (var form in forms)
        {
            var mapper = MapperFor(sourceType, destinationType, map => map.ForMember(nameof(IDestination.Other), form));

            var destination = Map(mapper, sourceType, destinationType);

            destination.Value.ShouldBe(3);
            destination.Other.ShouldBe(103);
        }
    }
    public class ValueIsThree : IMemberCondition<object, object, object>
    {
        public bool ShouldMap(object source, object destination, object sourceMember, object destinationMember, ResolutionContext context) => Read(source) == 3;
    }
    public class SourceIsThree : IMemberPreCondition<object, object>
    {
        public bool ShouldMap(object source, object destination, ResolutionContext context) => Read(source) == 3;
    }
    [Theory, MemberData(nameof(ValueTypes))]
    public void Conditions_and_preconditions_work_as_before(Type sourceType, Type destinationType)
    {
        Action<IMemberConfigurationExpression>[] forms =
        [
            o => o.Condition((source, destination, sourceMember, destinationMember, context) => Read(source) == 3 && destination.GetType() == destinationType),
            o => o.Condition((source, destination, sourceMember, destinationMember) => Read(source) == 3 && destination.GetType() == destinationType),
            o => o.Condition((source, destination, sourceMember) => Read(source) == 3 && destination.GetType() == destinationType),
            o => o.Condition((source, destination) => Read(source) == 3 && destination.GetType() == destinationType),
            o => o.Condition(source => Read(source) == 3),
            o => o.Condition(typeof(ValueIsThree)),
            o => o.PreCondition(source => Read(source) == 3),
            o => o.PreCondition((source, context) => Read(source) == 3),
            o => o.PreCondition((source, destination, context) => Read(source) == 3 && destination.GetType() == destinationType),
            o => o.PreCondition(typeof(SourceIsThree)),
        ];
        foreach (var form in forms)
        {
            IMapper[] mappers =
            [
                MapperFor(sourceType, destinationType, map => map.ForMember(nameof(IDestination.Value), form)),
                MapperFor(sourceType, destinationType, map => map.ForAllMembers(form)),
            ];
            foreach (var mapper in mappers)
            {
                Map(mapper, sourceType, destinationType, 3).Value.ShouldBe(3);
                Map(mapper, sourceType, destinationType, 4).Value.ShouldBe(0);
            }
        }
    }
    public class WithConstructor(int value) : IDestination
    {
        public int Value { get; } = value;
        public int Other { get; set; }
    }
    public struct StructWithConstructor(int value) : IDestination
    {
        public int Value { get; } = value;
        public int Other { get; set; }
    }
    [Theory, InlineData(typeof(StructSource), typeof(WithConstructor)), InlineData(typeof(ClassSource), typeof(StructWithConstructor)), InlineData(typeof(StructSource), typeof(StructWithConstructor))]
    public void A_constructor_parameter_in_every_form(Type sourceType, Type destinationType)
    {
        (Action<Configuration.ICtorParamConfigurationExpression<object>> Configure, int Expected)[] forms =
        [
            // This one failed with a source that is a value type.
            (o => o.MapFrom(source => Read(source) + 100), 103),
            (o => o.MapFrom((source, context) => Read(source) + 100), 103),
            (o => o.MapFrom(nameof(ISource.Value)), 3),
        ];
        foreach (var (configure, expected) in forms)
        {
            var mapper = MapperFor(sourceType, destinationType, map => map.ForCtorParam("value", configure));

            Map(mapper, sourceType, destinationType).Value.ShouldBe(expected);
        }
    }

    // Counts where a plan boxes a value of the given type.
    sealed class Boxes(Type valueType) : ExpressionVisitor
    {
        public int Count;
        protected override Expression VisitUnary(UnaryExpression node)
        {
            if (node.NodeType == ExpressionType.Convert && node.Operand.Type == valueType && !node.Type.IsValueType)
            {
                Count++;
            }
            return base.VisitUnary(node);
        }
        public static int In(MapperConfiguration configuration, Type sourceType, Type destinationType)
        {
            Boxes boxes = new(sourceType);
            boxes.Visit(configuration.BuildExecutionPlan(sourceType, destinationType));
            return boxes.Count;
        }
    }
    [Fact]
    public void A_member_called_on_the_source_is_called_on_the_value_itself_as_before()
    {
        // This worked before, and the plan is what it was: nothing is boxed.
        var configuration = new MapperConfiguration(cfg => cfg.CreateMap(typeof(StructSource), typeof(ClassDestination))
            .ForMember(nameof(ClassDestination.Other), o => o.MapFrom(source => source.ToString().Length + (source is ISource ? 100 : 0))));
        configuration.AssertConfigurationIsValid();

        Boxes.In(configuration, typeof(StructSource), typeof(ClassDestination)).ShouldBe(0);
        configuration.CreateMapper().Map<ClassDestination>(new StructSource { Value = 3 }).Other.ShouldBe(new StructSource().ToString().Length + 100);
    }
    [Fact]
    public void A_cast_of_the_source_to_its_type_is_the_value_itself()
    {
        var configuration = new MapperConfiguration(cfg => cfg.CreateMap(typeof(StructSource), typeof(ClassDestination))
            .ConstructUsing(source => new ClassDestination { Other = ((StructSource)source).Value + 100 })
            .ForMember(nameof(ClassDestination.Other), o => o.Ignore())
            .ForMember(nameof(ClassDestination.Value), o => o.MapFrom(source => ((StructSource)source).Value * 2)));
        configuration.AssertConfigurationIsValid();

        Boxes.In(configuration, typeof(StructSource), typeof(ClassDestination)).ShouldBe(0);
        var destination = configuration.CreateMapper().Map<ClassDestination>(new StructSource { Value = 3 });
        destination.Value.ShouldBe(6);
        destination.Other.ShouldBe(103);
    }
    [Fact]
    public void A_source_that_is_passed_on_is_boxed()
    {
        var configuration = new MapperConfiguration(cfg => cfg.CreateMap(typeof(StructSource), typeof(ClassDestination))
            .ForMember(nameof(ClassDestination.Other), o => o.MapFrom(source => Read(source) + 100)));

        Boxes.In(configuration, typeof(StructSource), typeof(ClassDestination)).ShouldBe(1);
    }
    [Fact]
    public void A_map_of_classes_is_not_changed()
    {
        var configuration = new MapperConfiguration(cfg => cfg.CreateMap(typeof(ClassSource), typeof(ClassDestination))
            .ConstructUsing((source, context) => new ClassDestination())
            .BeforeMap((source, destination) => { })
            .AfterMap((source, destination) => ((ClassDestination)destination).Other = Read(source) + 100)
            .ForMember(nameof(ClassDestination.Other), o => o.Ignore()));
        configuration.AssertConfigurationIsValid();

        // The source and the destination go into the lambdas as they are, without a conversion.
        ConversionsToObject.In(configuration.BuildExecutionPlan(typeof(ClassSource), typeof(ClassDestination))).ShouldBe(0);
        configuration.CreateMapper().Map<ClassDestination>(new ClassSource { Value = 3 }).Other.ShouldBe(103);
    }
    sealed class ConversionsToObject : ExpressionVisitor
    {
        int _count;
        protected override Expression VisitUnary(UnaryExpression node)
        {
            if (node.NodeType == ExpressionType.Convert && node.Type == typeof(object))
            {
                _count++;
            }
            return base.VisitUnary(node);
        }
        public static int In(Expression plan)
        {
            ConversionsToObject conversions = new();
            conversions.Visit(plan);
            return conversions._count;
        }
    }

    [Fact]
    public void ForAllMaps_with_maps_of_value_types()
    {
        List<Call> calls = [];
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<StructSource, ClassDestination>().ForMember(d => d.Other, o => o.Ignore());
            cfg.CreateMap<ClassSource, StructDestination>().ForMember(d => d.Other, o => o.Ignore());
            cfg.CreateMap<StructSource, StructDestination>().ForMember(d => d.Other, o => o.Ignore());
            cfg.ForAllMaps((typeMap, map) => map.AfterMap((source, destination) => calls.Add(Describe(source, destination))));
        });
        configuration.AssertConfigurationIsValid();
        var mapper = configuration.CreateMapper();

        mapper.Map<ClassDestination>(new StructSource { Value = 1 }).Value.ShouldBe(1);
        mapper.Map<StructDestination>(new ClassSource { Value = 2 }).Value.ShouldBe(2);
        mapper.Map<StructDestination>(new StructSource { Value = 3 }).Value.ShouldBe(3);

        calls.ShouldBe(new Call[]
        {
            new(typeof(StructSource), 1, typeof(ClassDestination), 1),
            new(typeof(ClassSource), 2, typeof(StructDestination), 2),
            new(typeof(StructSource), 3, typeof(StructDestination), 3),
        });
    }
    public struct Wrapper<T>
    {
        public T Value { get; set; }
    }
    public class WrapperDto<T>
    {
        public T Value { get; set; }
        public int Other { get; set; }
    }
    [Fact]
    public void An_open_generic_map_closed_with_a_value_type()
    {
        var configuration = new MapperConfiguration(cfg => cfg.CreateMap(typeof(Wrapper<>), typeof(WrapperDto<>))
            .ForMember("Other", o => o.Ignore())
            .ConstructUsing((source, context) => new WrapperDto<int> { Other = ((Wrapper<int>)source).Value + 100 })
            .AfterMap((source, destination) => ((WrapperDto<int>)destination).Other += ((Wrapper<int>)source).Value));
        configuration.AssertConfigurationIsValid();

        var destination = configuration.CreateMapper().Map<WrapperDto<int>>(new Wrapper<int> { Value = 3 });

        destination.Value.ShouldBe(3);
        destination.Other.ShouldBe(106);
    }
    [Fact]
    public void A_map_of_a_struct_that_inherits_from_the_map_of_an_interface()
    {
        // The generic API: the lambdas of the base map take the interface, and the derived map has the struct.
        List<int> before = [];
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<ISource, ClassDestination>()
                .ForMember(d => d.Other, o => o.MapFrom(source => Read(source) + 100))
                .BeforeMap((source, destination) => before.Add(source.Value))
                .Include<StructSource, ClassDestination>();
            cfg.CreateMap<StructSource, ClassDestination>();
        });
        configuration.AssertConfigurationIsValid();

        var destination = configuration.CreateMapper().Map<ClassDestination>(new StructSource { Value = 3 });

        destination.Value.ShouldBe(3);
        destination.Other.ShouldBe(103);
        before.ShouldBe([3]);
    }
    [Fact]
    public void An_inherited_member_that_is_read_from_the_interface_is_read_from_the_value_itself_as_before()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<ISource, ClassDestination>().ForMember(d => d.Other, o => o.MapFrom(source => source.Value + 100)).Include<StructSource, ClassDestination>();
            cfg.CreateMap<StructSource, ClassDestination>();
        });
        configuration.AssertConfigurationIsValid();

        Boxes.In(configuration, typeof(StructSource), typeof(ClassDestination)).ShouldBe(0);
        configuration.CreateMapper().Map<ClassDestination>(new StructSource { Value = 3 }).Other.ShouldBe(103);
    }
    [Fact]
    public void A_source_that_is_a_nullable_value_type()
    {
        List<object> sources = [];
        var configuration = new MapperConfiguration(cfg => cfg.CreateMap(typeof(StructSource?), typeof(ClassDestination))
            .ForMember(nameof(ClassDestination.Other), o => o.Ignore())
            .ForMember(nameof(ClassDestination.Value), o => o.MapFrom(source => ((StructSource?)source).Value.Value))
            .BeforeMap((source, destination) => sources.Add(source)));
        configuration.AssertConfigurationIsValid();
        var mapper = configuration.CreateMapper();

        mapper.Map<StructSource?, ClassDestination>(new StructSource { Value = 3 }).Value.ShouldBe(3);
        mapper.Map<StructSource?, ClassDestination>(null).ShouldBeNull();

        // Boxed, a nullable with a value is its value. A null source is not mapped, so the action is not called for it.
        sources.ShouldBe(new object[] { new StructSource { Value = 3 } });
    }
    [Fact]
    public void A_transformer_of_a_member_that_is_a_value_type()
    {
        // The same lambda of object, for the value of a member: it failed for a map of classes too.
        var mapper = MapperFor(typeof(ClassSource), typeof(ClassDestination), map => map.ForMember(nameof(ClassDestination.Value), o => o.AddTransform(value => AddHundred(value))));

        Map(mapper, typeof(ClassSource), typeof(ClassDestination)).Value.ShouldBe(103);
    }
}
