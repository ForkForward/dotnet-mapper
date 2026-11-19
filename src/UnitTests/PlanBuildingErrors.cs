using ForkForward.Mapper.Internal.Mappers;
using ForkForward.Mapper.QueryableExtensions.Impl;

namespace ForkForward.Mapper.UnitTests;

// A mapping plan is an expression tree that is built per type map. When a part of it cannot be built, what the
// caller got was whatever System.Linq.Expressions, or a custom mapper, resolver or converter, had thrown: nothing
// in it said which of the maps of the application it came from. It has to name the map and the member.
public class PlanBuildingErrors
{
    const string PlanError = "Error building the mapping plan.";
    const string ProjectionError = "Error building the projection.";

    // Broken -> BrokenDto is the pair these configurations cannot build a plan for; see the mappers below.
    public class Broken { }
    public class BrokenDto { }
    public class Source { public Broken Value { get; set; } }
    public class Destination { public BrokenDto Value { get; set; } }
    public class FieldDestination { public BrokenDto Value; }
    public class PathDestination { public Destination Inner { get; set; } }
    public class ConstructorDestination(BrokenDto value) { public BrokenDto Value { get; } = value; }
    public class ObjectSource { public object Value { get; set; } }
    public class Outer { public Source Inner { get; set; } }
    public class OuterDto { public Destination Inner { get; set; } }
    public class ListOuter { public List<Source> Items { get; set; } }
    public class ListOuterDto { public List<Destination> Items { get; set; } }
    public class BrokenList { public List<Broken> Items { get; set; } }
    public class BrokenListDto { public List<BrokenDto> Items { get; set; } }
    public class Wrapper<T> { public T Value { get; set; } }
    public class WrapperDto<T> { public T Value { get; set; } }
    public class WrapperOuter { public Wrapper<Broken> Item { get; set; } }
    public class WrapperOuterDto { public WrapperDto<BrokenDto> Item { get; set; } }
    public class Plain { public int Value { get; set; } }
    public class PlainDto { public int Value { get; set; } }

    abstract class BrokenPairMapper : IObjectMapper
    {
        public bool IsMatch(TypePair context) => context == new TypePair(typeof(Broken), typeof(BrokenDto));
        public abstract Expression MapExpression(IGlobalConfiguration configuration, ProfileMap profileMap, MemberMap memberMap, Expression sourceExpression, Expression destExpression);
    }
    sealed class ThrowingMapper(Exception error = null) : BrokenPairMapper
    {
        public Exception Error { get; } = error ?? new InvalidOperationException("The mapper cannot build this.");
        public override Expression MapExpression(IGlobalConfiguration configuration, ProfileMap profileMap, MemberMap memberMap, Expression sourceExpression, Expression destExpression) =>
            throw Error;
    }
    // What it returns is not a BrokenDto, so the plan builder fails inside System.Linq.Expressions.
    sealed class WrongTypeMapper : BrokenPairMapper
    {
        public override Expression MapExpression(IGlobalConfiguration configuration, ProfileMap profileMap, MemberMap memberMap, Expression sourceExpression, Expression destExpression) =>
            Expression.Constant("not a BrokenDto");
    }
    // The plan can be built, but not compiled: nothing declares the variable.
    sealed class UndeclaredVariableMapper : BrokenPairMapper
    {
        public override Expression MapExpression(IGlobalConfiguration configuration, ProfileMap profileMap, MemberMap memberMap, Expression sourceExpression, Expression destExpression) =>
            Expression.Variable(typeof(BrokenDto), "undeclared");
    }
    sealed class WorkingMapper : BrokenPairMapper
    {
        public override Expression MapExpression(IGlobalConfiguration configuration, ProfileMap profileMap, MemberMap memberMap, Expression sourceExpression, Expression destExpression) =>
            Expression.New(typeof(BrokenDto));
    }
    // The mappers are asked whether they match the types of a member before the plan of its map is built.
    sealed class FailingMatchMapper(Exception error) : IObjectMapper
    {
        public bool IsMatch(TypePair context) => context.SourceType == typeof(Broken) ? throw error : false;
        public Expression MapExpression(IGlobalConfiguration configuration, ProfileMap profileMap, MemberMap memberMap, Expression sourceExpression, Expression destExpression) =>
            throw new NotImplementedException();
    }
    sealed class ThrowingResolver(Exception error) : Execution.IValueResolver
    {
        public Expression GetExpression(IGlobalConfiguration configuration, MemberMap memberMap, Expression source, Expression destination, Expression destinationMember) =>
            throw error;
        public MemberInfo GetSourceMember(MemberMap memberMap) => null;
        public Type ResolvedType => typeof(int);
    }
    sealed class ThrowingConverter(Exception error) : Execution.TypeConverter
    {
        public override Expression GetExpression(IGlobalConfiguration configuration, ParameterExpression[] parameters) => throw error;
        public override LambdaExpression ProjectToExpression => throw error;
    }
    abstract class BrokenPairProjectionMapper : IProjectionMapper
    {
        public bool IsMatch(TypePair context) => context == new TypePair(typeof(Broken), typeof(BrokenDto));
        public abstract Expression Project(IGlobalConfiguration configuration, in ProjectionRequest request, Expression resolvedSource, LetPropertyMaps letPropertyMaps);
    }
    sealed class ThrowingProjectionMapper(Exception error = null) : BrokenPairProjectionMapper
    {
        public Exception Error { get; } = error ?? new InvalidOperationException("The projection mapper cannot build this.");
        public override Expression Project(IGlobalConfiguration configuration, in ProjectionRequest request, Expression resolvedSource, LetPropertyMaps letPropertyMaps) =>
            throw Error;
    }
    sealed class WrongTypeProjectionMapper : BrokenPairProjectionMapper
    {
        public override Expression Project(IGlobalConfiguration configuration, in ProjectionRequest request, Expression resolvedSource, LetPropertyMaps letPropertyMaps) =>
            Expression.Constant("not a BrokenDto");
    }
    sealed class WorkingProjectionMapper : BrokenPairProjectionMapper
    {
        public override Expression Project(IGlobalConfiguration configuration, in ProjectionRequest request, Expression resolvedSource, LetPropertyMaps letPropertyMaps) =>
            Expression.New(typeof(BrokenDto));
    }

    static MapperConfiguration Configure(IObjectMapper mapper, Action<IMapperConfigurationExpression> configure) => new(cfg =>
    {
        cfg.Internal().Mappers.Insert(0, mapper);
        configure(cfg);
    });
    static MapperConfiguration Configure(IProjectionMapper mapper, Action<IMapperConfigurationExpression> configure) => new(cfg =>
    {
        cfg.Internal().ProjectionMappers.Insert(0, mapper);
        configure(cfg);
    });
    static IQueryable<TSource> Query<TSource>(params TSource[] items) => items.AsQueryable();
    static AutoMapperMappingException ErrorOf(Action action, string sentence = PlanError)
    {
        var exception = Should.Throw<AutoMapperMappingException>(action);
        exception.Message.ShouldStartWith(sentence, Case.Sensitive);
        return exception;
    }
    // The type map, and the member when the error belongs to one: both on the exception and in its message.
    static void ShouldName<TSource, TDestination>(AutoMapperMappingException exception, string member = null)
    {
        exception.TypeMap.ShouldNotBeNull();
        exception.TypeMap.Types.ShouldBe(new TypePair(typeof(TSource), typeof(TDestination)));
        exception.Message.ShouldContain($"{typeof(TSource).FullName} -> {typeof(TDestination).FullName}", Case.Sensitive);
        if (member == null)
        {
            exception.MemberMap.ShouldBeNull();
            exception.Message.ShouldNotContain("Destination Member:");
            return;
        }
        exception.MemberMap.ShouldNotBeNull();
        exception.MemberMap.DestinationName.ShouldBe(member);
        exception.Message.ShouldContain("Destination Member:" + Environment.NewLine + exception.MemberMap, Case.Sensitive);
    }

    // When the configuration is created.

    [Fact]
    public void A_property_names_the_map_and_the_member()
    {
        var mapper = new ThrowingMapper();

        var exception = ErrorOf(() => Configure(mapper, cfg => cfg.CreateMap<Source, Destination>()));

        ShouldName<Source, Destination>(exception, "Value");
        exception.MemberMap.ShouldBeOfType<PropertyMap>();
        exception.InnerException.ShouldBeSameAs(mapper.Error);
    }

    [Fact]
    public void An_expression_that_cannot_be_built_keeps_the_exception_of_the_framework()
    {
        var exception = ErrorOf(() => Configure(new WrongTypeMapper(), cfg => cfg.CreateMap<Source, Destination>()));

        ShouldName<Source, Destination>(exception, "Value");
        // No coercion operator is defined between types 'System.String' and '...BrokenDto'.
        exception.InnerException.ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public void A_field_names_the_map_and_the_member()
    {
        var exception = ErrorOf(() => Configure(new ThrowingMapper(), cfg => cfg.CreateMap<Source, FieldDestination>()));

        ShouldName<Source, FieldDestination>(exception, "Value");
        exception.MemberMap.ShouldBeOfType<PropertyMap>().DestinationMember.ShouldBe(typeof(FieldDestination).GetField("Value"));
    }

    [Fact]
    public void A_resolver_that_cannot_build_its_expression_names_the_member()
    {
        var error = new NotSupportedException("The resolver cannot build this.");

        var exception = ErrorOf(() => new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Plain, PlainDto>();
            cfg.Internal().ForAllPropertyMaps(propertyMap => true, (propertyMap, _) => propertyMap.SetResolver(new ThrowingResolver(error)));
        }));

        ShouldName<Plain, PlainDto>(exception, "Value");
        exception.InnerException.ShouldBeSameAs(error);
    }

    [Fact]
    public void A_mapper_that_fails_when_asked_whether_it_matches_names_the_member()
    {
        var error = new NotSupportedException("The mapper cannot tell.");

        var exception = ErrorOf(() => Configure(new FailingMatchMapper(error), cfg => cfg.CreateMap<Source, Destination>()));

        ShouldName<Source, Destination>(exception, "Value");
        exception.InnerException.ShouldBeSameAs(error);
    }

    [Fact]
    public void A_path_names_the_map_and_the_path()
    {
        var mapper = new ThrowingMapper();

        var exception = ErrorOf(() => Configure(mapper, cfg =>
            cfg.CreateMap<Source, PathDestination>().ForPath(destination => destination.Inner.Value, o => o.MapFrom(source => source.Value))));

        ShouldName<Source, PathDestination>(exception, "Inner.Value");
        exception.MemberMap.ShouldBeOfType<PathMap>();
        exception.InnerException.ShouldBeSameAs(mapper.Error);
    }

    [Fact]
    public void A_constructor_parameter_names_the_map_and_the_parameter()
    {
        var mapper = new ThrowingMapper();

        var exception = ErrorOf(() => Configure(mapper, cfg => cfg.CreateMap<Source, ConstructorDestination>()));

        ShouldName<Source, ConstructorDestination>(exception, "value");
        exception.MemberMap.ShouldBeOfType<ConstructorParameterMap>().Parameter.Member.DeclaringType.ShouldBe(typeof(ConstructorDestination));
        exception.InnerException.ShouldBeSameAs(mapper.Error);
    }

    // What belongs to the type map as a whole names the map alone. The public API does not let an expression
    // written for other types through, so these tests plant one in the type map.
    static AutoMapperMappingException TypeMapErrorOf(Action<TypeMap> plant) => ErrorOf(() => new MapperConfiguration(cfg =>
    {
        cfg.CreateMap<Plain, PlainDto>();
        cfg.Internal().ForAllMaps((typeMap, _) => plant(typeMap));
    }));

    [Fact]
    public void A_type_converter_names_the_map_alone()
    {
        var error = new NotSupportedException("The converter cannot build this.");

        var exception = TypeMapErrorOf(typeMap => typeMap.TypeConverter = new ThrowingConverter(error));

        ShouldName<Plain, PlainDto>(exception);
        exception.InnerException.ShouldBeSameAs(error);
    }

    [Fact]
    public void The_construction_of_the_destination_names_the_map_alone()
    {
        Expression<Func<string, PlainDto>> constructor = text => new PlainDto { Value = text.Length };

        var exception = TypeMapErrorOf(typeMap => typeMap.CustomCtorFunction = constructor);

        ShouldName<Plain, PlainDto>(exception);
        // Property 'Int32 Length' is not defined for type '...Plain'.
        exception.InnerException.ShouldBeOfType<ArgumentException>();
    }

    [Fact]
    public void A_before_map_action_names_the_map_alone()
    {
        Expression<Action<string, string>> action = (source, destination) => source.CompareTo(destination);

        var exception = TypeMapErrorOf(typeMap => typeMap.AddBeforeMapAction(action));

        ShouldName<Plain, PlainDto>(exception);
        exception.InnerException.ShouldBeOfType<ArgumentException>();
    }

    [Fact]
    public void An_after_map_action_names_the_map_alone()
    {
        Expression<Action<string, string>> action = (source, destination) => source.CompareTo(destination);

        var exception = TypeMapErrorOf(typeMap => typeMap.AddAfterMapAction(action));

        ShouldName<Plain, PlainDto>(exception);
        exception.InnerException.ShouldBeOfType<ArgumentException>();
    }

    // A member that is mapped by another type map: the plan of that map is built on the way, inside the plan
    // of the outer one. The exception names the map and the member that failed, whichever map was built first.

    [Fact]
    public void A_member_of_a_nested_map_names_the_nested_map()
    {
        var mapper = new ThrowingMapper();

        var outerFirst = ErrorOf(() => Configure(mapper, cfg =>
        {
            cfg.CreateMap<Outer, OuterDto>();
            cfg.CreateMap<Source, Destination>();
        }));
        var nestedFirst = ErrorOf(() => Configure(mapper, cfg =>
        {
            cfg.CreateMap<Source, Destination>();
            cfg.CreateMap<Outer, OuterDto>();
        }));

        ShouldName<Source, Destination>(outerFirst, "Value");
        outerFirst.InnerException.ShouldBeSameAs(mapper.Error);
        ShouldName<Source, Destination>(nestedFirst, "Value");
        nestedFirst.InnerException.ShouldBeSameAs(mapper.Error);
    }

    [Fact]
    public void A_member_of_a_map_nested_in_a_collection_names_the_nested_map()
    {
        var mapper = new ThrowingMapper();

        var exception = ErrorOf(() => Configure(mapper, cfg =>
        {
            cfg.CreateMap<ListOuter, ListOuterDto>();
            cfg.CreateMap<Source, Destination>();
        }));

        ShouldName<Source, Destination>(exception, "Value");
        exception.InnerException.ShouldBeSameAs(mapper.Error);
    }

    [Fact]
    public void A_collection_whose_elements_cannot_be_mapped_names_the_collection()
    {
        var mapper = new ThrowingMapper();

        var exception = ErrorOf(() => Configure(mapper, cfg => cfg.CreateMap<BrokenList, BrokenListDto>()));

        ShouldName<BrokenList, BrokenListDto>(exception, "Items");
        exception.InnerException.ShouldBeSameAs(mapper.Error);
    }

    [Fact]
    public void A_closed_generic_map_used_by_a_member_names_itself()
    {
        var mapper = new ThrowingMapper();

        var exception = ErrorOf(() => Configure(mapper, cfg =>
        {
            cfg.CreateMap(typeof(Wrapper<>), typeof(WrapperDto<>));
            cfg.CreateMap<WrapperOuter, WrapperOuterDto>();
        }));

        ShouldName<Wrapper<Broken>, WrapperDto<BrokenDto>>(exception, "Value");
        exception.InnerException.ShouldBeSameAs(mapper.Error);
    }

    // Later, when a plan is built on first use.

    [Fact]
    public void A_closed_generic_map_names_itself_and_the_member_on_first_use()
    {
        var thrower = new ThrowingMapper();
        var mapper = Configure(thrower, cfg => cfg.CreateMap(typeof(Wrapper<>), typeof(WrapperDto<>))).CreateMapper();

        var exception = ErrorOf(() => mapper.Map<WrapperDto<BrokenDto>>(new Wrapper<Broken>()));

        ShouldName<Wrapper<Broken>, WrapperDto<BrokenDto>>(exception, "Value");
        exception.InnerException.ShouldBeSameAs(thrower.Error);
        // The same call again says the same, and the other maps closed from the same open map are not affected.
        ShouldName<Wrapper<Broken>, WrapperDto<BrokenDto>>(ErrorOf(() => mapper.Map<WrapperDto<BrokenDto>>(new Wrapper<Broken>())), "Value");
        mapper.Map<WrapperDto<int>>(new Wrapper<int> { Value = 42 }).Value.ShouldBe(42);
    }

    [Fact]
    public void BuildExecutionPlan_names_the_map_and_the_member()
    {
        var mapper = new ThrowingMapper();
        var configuration = Configure(mapper, cfg => cfg.CreateMap(typeof(Wrapper<>), typeof(WrapperDto<>)));

        var exception = ErrorOf(() => configuration.BuildExecutionPlan(typeof(Wrapper<Broken>), typeof(WrapperDto<BrokenDto>)));

        ShouldName<Wrapper<Broken>, WrapperDto<BrokenDto>>(exception, "Value");
        exception.InnerException.ShouldBeSameAs(mapper.Error);
    }

    [Fact]
    public void A_pair_without_a_type_map_names_the_types()
    {
        var mapper = new ThrowingMapper();
        var configuration = Configure(mapper, cfg => { });

        var mapping = ErrorOf(() => configuration.CreateMapper().Map<BrokenDto>(new Broken()));
        var plan = ErrorOf(() => configuration.BuildExecutionPlan(typeof(Broken), typeof(BrokenDto)));

        foreach (var exception in new[] { mapping, plan })
        {
            exception.Types.ShouldBe(new TypePair(typeof(Broken), typeof(BrokenDto)));
            exception.Message.ShouldContain($"{typeof(Broken).FullName} -> {typeof(BrokenDto).FullName}", Case.Sensitive);
            exception.TypeMap.ShouldBeNull();
            exception.MemberMap.ShouldBeNull();
            exception.Message.ShouldNotContain("Destination Member:");
            exception.InnerException.ShouldBeSameAs(mapper.Error);
        }
    }

    [Fact]
    public void A_map_resolved_at_run_time_names_the_types_and_the_member_it_was_resolved_for()
    {
        var thrower = new ThrowingMapper();
        var mapper = Configure(thrower, cfg => cfg.CreateMap<ObjectSource, Destination>()).CreateMapper();

        var exception = Should.Throw<AutoMapperMappingException>(() => mapper.Map<Destination>(new ObjectSource { Value = new Broken() }));

        // The plan for Broken -> BrokenDto is built while ObjectSource is being mapped. A compiled plan wraps the
        // error once more, as an error while mapping Value; an interpreted plan does not (see InterpretedPlans.cs).
        var planError = exception.Message.StartsWith(PlanError) ? exception : exception.InnerException.ShouldBeOfType<AutoMapperMappingException>();
        planError.Message.ShouldStartWith(PlanError, Case.Sensitive);
        planError.Types.ShouldBe(new TypePair(typeof(Broken), typeof(BrokenDto)));
        planError.MemberMap.ShouldNotBeNull();
        planError.MemberMap.DestinationName.ShouldBe("Value");
        planError.MemberMap.TypeMap.Types.ShouldBe(new TypePair(typeof(ObjectSource), typeof(Destination)));
        planError.InnerException.ShouldBeSameAs(thrower.Error);
    }

    [Fact]
    public void A_plan_that_cannot_be_compiled_names_the_types()
    {
        var mapper = Configure(new UndeclaredVariableMapper(), cfg => { }).CreateMapper();

        var exception = ErrorOf(() => mapper.Map<BrokenDto>(new Broken()));

        exception.Types.ShouldBe(new TypePair(typeof(Broken), typeof(BrokenDto)));
        // variable 'undeclared' of type '...BrokenDto' referenced from scope '', but it is not defined
        exception.InnerException.ShouldBeOfType<InvalidOperationException>();
    }

    // What must not change.

    [Fact]
    public void The_exceptions_of_the_library_are_passed_on_as_they_are()
    {
        Exception[] errors = [new AutoMapperMappingException("Thrown on purpose."), new AutoMapperConfigurationException("Thrown on purpose."), new DuplicateTypeMapConfigurationException([])];
        foreach (var error in errors)
        {
            var thrown = Should.Throw<Exception>(() => Configure(new ThrowingMapper(error), cfg =>
            {
                cfg.CreateMap<Outer, OuterDto>();
                cfg.CreateMap<Source, Destination>();
            }));

            thrown.ShouldBeSameAs(error);
        }
    }

    [Fact]
    public void Mapping_a_projection_map_is_reported_as_before()
    {
        var exception = Should.Throw<AutoMapperConfigurationException>(() => new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Outer, OuterDto>();
            cfg.CreateProjection<Source, Destination>();
        }));

        exception.Message.ShouldBe("CreateProjection works with ProjectTo, not with Map.");
    }

    [Fact]
    public void The_same_maps_work_when_the_pair_can_be_mapped()
    {
        var mapper = Configure(new WorkingMapper(), cfg =>
        {
            cfg.CreateMap<Source, Destination>();
            cfg.CreateMap<Source, FieldDestination>();
            cfg.CreateMap<Source, PathDestination>().ForPath(destination => destination.Inner.Value, o => o.MapFrom(source => source.Value));
            cfg.CreateMap<Source, ConstructorDestination>();
            cfg.CreateMap<ObjectSource, Destination>();
            cfg.CreateMap<Outer, OuterDto>();
            cfg.CreateMap<ListOuter, ListOuterDto>();
            cfg.CreateMap<BrokenList, BrokenListDto>();
            cfg.CreateMap(typeof(Wrapper<>), typeof(WrapperDto<>));
            cfg.CreateMap<WrapperOuter, WrapperOuterDto>();
        }).CreateMapper();
        var source = new Source { Value = new Broken() };

        mapper.Map<Destination>(source).Value.ShouldNotBeNull();
        mapper.Map<FieldDestination>(source).Value.ShouldNotBeNull();
        mapper.Map<PathDestination>(source).Inner.Value.ShouldNotBeNull();
        mapper.Map<ConstructorDestination>(source).Value.ShouldNotBeNull();
        mapper.Map<Destination>(new ObjectSource { Value = new Broken() }).Value.ShouldNotBeNull();
        mapper.Map<OuterDto>(new Outer { Inner = source }).Inner.Value.ShouldNotBeNull();
        mapper.Map<ListOuterDto>(new ListOuter { Items = [source] }).Items.Single().Value.ShouldNotBeNull();
        mapper.Map<BrokenListDto>(new BrokenList { Items = [new Broken()] }).Items.Single().ShouldNotBeNull();
        mapper.Map<WrapperDto<BrokenDto>>(new Wrapper<Broken> { Value = new Broken() }).Value.ShouldNotBeNull();
        mapper.Map<WrapperOuterDto>(new WrapperOuter { Item = new Wrapper<Broken> { Value = new Broken() } }).Item.Value.ShouldNotBeNull();
        mapper.Map<BrokenDto>(new Broken()).ShouldNotBeNull();
        mapper.ConfigurationProvider.BuildExecutionPlan(typeof(Broken), typeof(BrokenDto)).ShouldNotBeNull();
    }

    // ProjectTo builds an expression of its own for the query, the first time it is asked for.

    [Fact]
    public void A_projected_property_names_the_map_and_the_member()
    {
        var mapper = new ThrowingProjectionMapper();
        var configuration = Configure(mapper, cfg => cfg.CreateProjection<Source, Destination>());

        var exception = ErrorOf(() => Query<Source>().ProjectTo<Destination>(configuration), ProjectionError);

        ShouldName<Source, Destination>(exception, "Value");
        exception.MemberMap.ShouldBeOfType<PropertyMap>();
        exception.InnerException.ShouldBeSameAs(mapper.Error);
        // The same query again says the same.
        ShouldName<Source, Destination>(ErrorOf(() => Query<Source>().ProjectTo<Destination>(configuration), ProjectionError), "Value");
    }

    [Fact]
    public void A_projection_of_the_wrong_type_names_the_map_and_the_member()
    {
        var configuration = Configure(new WrongTypeProjectionMapper(), cfg => cfg.CreateProjection<Source, Destination>());

        var exception = ErrorOf(() => Query<Source>().ProjectTo<Destination>(configuration), ProjectionError);

        ShouldName<Source, Destination>(exception, "Value");
        // Argument types do not match
        exception.InnerException.ShouldBeOfType<ArgumentException>();
    }

    [Fact]
    public void A_projected_constructor_parameter_names_the_map_and_the_parameter()
    {
        var mapper = new ThrowingProjectionMapper();
        var configuration = Configure(mapper, cfg => cfg.CreateProjection<Source, ConstructorDestination>());

        var exception = ErrorOf(() => Query<Source>().ProjectTo<ConstructorDestination>(configuration), ProjectionError);

        ShouldName<Source, ConstructorDestination>(exception, "value");
        exception.MemberMap.ShouldBeOfType<ConstructorParameterMap>();
        exception.InnerException.ShouldBeSameAs(mapper.Error);
    }

    [Fact]
    public void A_projection_as_a_whole_names_the_map_alone()
    {
        var error = new NotSupportedException("The converter cannot build this.");
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.CreateProjection<Plain, PlainDto>();
            cfg.Internal().ForAllMaps((typeMap, _) => typeMap.TypeConverter = new ThrowingConverter(error));
        });

        var exception = ErrorOf(() => Query<Plain>().ProjectTo<PlainDto>(configuration), ProjectionError);

        ShouldName<Plain, PlainDto>(exception);
        exception.InnerException.ShouldBeSameAs(error);
    }

    [Fact]
    public void A_member_of_a_nested_projection_names_the_nested_map()
    {
        var mapper = new ThrowingProjectionMapper();
        var configuration = Configure(mapper, cfg =>
        {
            cfg.CreateProjection<Outer, OuterDto>();
            cfg.CreateProjection<ListOuter, ListOuterDto>();
            cfg.CreateProjection<Source, Destination>();
        });

        var nested = ErrorOf(() => Query<Outer>().ProjectTo<OuterDto>(configuration), ProjectionError);
        var nestedInCollection = ErrorOf(() => Query<ListOuter>().ProjectTo<ListOuterDto>(configuration), ProjectionError);

        ShouldName<Source, Destination>(nested, "Value");
        nested.InnerException.ShouldBeSameAs(mapper.Error);
        ShouldName<Source, Destination>(nestedInCollection, "Value");
        nestedInCollection.InnerException.ShouldBeSameAs(mapper.Error);
    }

    [Fact]
    public void The_error_for_a_member_that_no_projection_mapper_matches_is_as_before()
    {
        var configuration = new MapperConfiguration(cfg => cfg.CreateProjection<Source, Destination>());

        var exception = Should.Throw<AutoMapperMappingException>(() => Query<Source>().ProjectTo<Destination>(configuration));

        exception.Message.ShouldStartWith("Unable to create a map expression from Source.Value", Case.Sensitive);
        exception.InnerException.ShouldBeNull();
        ShouldName<Source, Destination>(exception, "Value");
    }

    [Fact]
    public void The_exceptions_of_the_library_are_passed_on_as_they_are_by_projections()
    {
        Exception[] errors = [new AutoMapperMappingException("Thrown on purpose."), new AutoMapperConfigurationException("Thrown on purpose."), new DuplicateTypeMapConfigurationException([])];
        foreach (var error in errors)
        {
            var configuration = Configure(new ThrowingProjectionMapper(error), cfg =>
            {
                cfg.CreateProjection<Outer, OuterDto>();
                cfg.CreateProjection<Source, Destination>();
            });

            Should.Throw<Exception>(() => Query<Outer>().ProjectTo<OuterDto>(configuration)).ShouldBeSameAs(error);
        }
    }

    // "Missing map from ... to ..." is thrown on purpose and says what is missing, but its type is the framework's
    // InvalidOperationException. It is passed on as it is at every level, not only for the types of the query itself.
    [Fact]
    public void A_missing_map_for_the_elements_of_a_collection_member_is_reported_as_before()
    {
        var configuration = new MapperConfiguration(cfg => cfg.CreateProjection<ListOuter, ListOuterDto>());

        var topLevel = Should.Throw<InvalidOperationException>(() => Query<Source>().ProjectTo<Destination>(configuration));
        var nested = Should.Throw<InvalidOperationException>(() => Query<ListOuter>().ProjectTo<ListOuterDto>(configuration));

        topLevel.GetType().ShouldBe(typeof(InvalidOperationException));
        nested.GetType().ShouldBe(typeof(InvalidOperationException));
        topLevel.Message.ShouldStartWith($"Missing map from {typeof(Source)} to {typeof(Destination)}.");
        nested.Message.ShouldBe(topLevel.Message);
    }

    [Fact]
    public void The_same_projections_work_when_the_pair_can_be_projected()
    {
        var configuration = Configure(new WorkingProjectionMapper(), cfg =>
        {
            cfg.CreateProjection<Source, Destination>();
            cfg.CreateProjection<Source, ConstructorDestination>();
            cfg.CreateProjection<Outer, OuterDto>();
            cfg.CreateProjection<ListOuter, ListOuterDto>();
        });
        var source = new Source { Value = new Broken() };

        Query(source).ProjectTo<Destination>(configuration).Single().Value.ShouldNotBeNull();
        Query(source).ProjectTo<ConstructorDestination>(configuration).Single().Value.ShouldNotBeNull();
        Query(new Outer { Inner = source }).ProjectTo<OuterDto>(configuration).Single().Inner.Value.ShouldNotBeNull();
        Query(new ListOuter { Items = [source] }).ProjectTo<ListOuterDto>(configuration).Single().Items.Single().Value.ShouldNotBeNull();
    }
}
