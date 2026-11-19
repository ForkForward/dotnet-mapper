namespace ForkForward.Mapper.UnitTests.DestinationFactories;

public class When_constructing_the_destination_with_a_factory : AutoMapperSpecBase
{
    class Source
    {
        public int Value { get; set; }
    }
    // No constructor the mapper could use: without the factory this map is not valid.
    class Destination(string createdBy)
    {
        public string CreatedBy { get; } = createdBy;
        public int Value { get; set; }
    }
    class Factory : IDestinationFactory<Source, Destination>
    {
        public Destination Create(Source source, ResolutionContext context) => new("factory for " + source.Value);
    }
    protected override MapperConfiguration CreateConfiguration() => new(cfg => cfg.CreateMap<Source, Destination>().ConstructUsing<Factory>());
    [Fact]
    public void Should_map_the_members_onto_the_object_the_factory_creates()
    {
        var destination = Mapper.Map<Destination>(new Source { Value = 42 });
        destination.CreatedBy.ShouldBe("factory for 42");
        destination.Value.ShouldBe(42);
    }
    [Fact]
    public void Should_create_every_element_of_a_collection() =>
        Mapper.Map<List<Destination>>(new[] { new Source { Value = 1 }, new Source { Value = 2 } }).Select(d => d.CreatedBy).ShouldBe(["factory for 1", "factory for 2"]);
    [Fact]
    public void Should_not_call_the_factory_for_an_existing_destination()
    {
        var existing = new Destination("caller");
        Mapper.Map(new Source { Value = 42 }, existing).ShouldBeSameAs(existing);
        existing.CreatedBy.ShouldBe("caller");
        existing.Value.ShouldBe(42);
    }
    [Fact]
    public void Should_not_create_a_destination_for_a_null_source() => Mapper.Map<Destination>(null).ShouldBeNull();
}

public class When_the_factory_type_is_passed_as_a_type : AutoMapperSpecBase
{
    class Source
    {
        public int Value { get; set; }
    }
    class Destination(string createdBy)
    {
        public string CreatedBy { get; } = createdBy;
        public int Value { get; set; }
    }
    class OtherDestination(string createdBy) : Destination(createdBy);
    class Factory : IDestinationFactory<Source, Destination>, IDestinationFactory<Source, OtherDestination>
    {
        public Destination Create(Source source, ResolutionContext context) => new("factory");
        OtherDestination IDestinationFactory<Source, OtherDestination>.Create(Source source, ResolutionContext context) => new("factory, other destination");
    }
    protected override MapperConfiguration CreateConfiguration() => new(cfg =>
    {
        cfg.CreateMap(typeof(Source), typeof(Destination)).ConstructUsing(typeof(Factory));
        cfg.CreateMap<Source, OtherDestination>().ConstructUsing(typeof(Factory));
    });
    [Fact]
    public void Should_work_for_a_map_created_from_types()
    {
        var destination = Mapper.Map<Destination>(new Source { Value = 42 });
        destination.CreatedBy.ShouldBe("factory");
        destination.Value.ShouldBe(42);
    }
    [Fact]
    public void Should_call_the_interface_declared_for_the_types_of_the_map()
    {
        var destination = Mapper.Map<OtherDestination>(new Source { Value = 42 });
        destination.CreatedBy.ShouldBe("factory, other destination");
        destination.Value.ShouldBe(42);
    }
}

public class When_a_destination_factory_takes_dependencies : AutoMapperSpecBase
{
    readonly List<Type> _created = [];
    class Source
    {
    }
    class Destination(string createdBy)
    {
        public string CreatedBy { get; } = createdBy;
    }
    class Factory(string dependency) : IDestinationFactory<Source, Destination>
    {
        public Destination Create(Source source, ResolutionContext context) => new(dependency);
    }
    protected override MapperConfiguration CreateConfiguration() => new(cfg =>
    {
        cfg.ConstructServicesUsing(type =>
        {
            _created.Add(type);
            return new Factory("configuration");
        });
        cfg.CreateMap<Source, Destination>().ConstructUsing<Factory>();
    });
    [Fact]
    public void Should_create_the_factory_with_the_service_constructor_of_the_configuration() =>
        Mapper.Map<Destination>(new Source()).CreatedBy.ShouldBe("configuration");
    [Fact]
    public void Should_create_the_factory_with_the_service_constructor_of_the_mapper() =>
        Mapper.ConfigurationProvider.CreateMapper(type => new Factory("mapper")).Map<Destination>(new Source()).CreatedBy.ShouldBe("mapper");
    [Fact]
    public void Should_create_the_factory_with_the_service_constructor_of_the_call() =>
        Mapper.Map<Source, Destination>(new Source(), opt => opt.ConstructServicesUsing(type => new Factory("call"))).CreatedBy.ShouldBe("call");
    [Fact]
    public void Should_create_a_factory_each_time_a_destination_is_created()
    {
        Mapper.Map<Destination>(new Source());
        Mapper.Map<Destination[]>(new[] { new Source(), new Source() });
        _created.ShouldBe([typeof(Factory), typeof(Factory), typeof(Factory)]);
    }
    [Fact]
    public void Should_not_create_a_factory_for_an_existing_destination()
    {
        Mapper.Map(new Source(), new Destination("caller"));
        _created.ShouldBeEmpty();
    }
}

public class When_the_service_constructor_cannot_create_the_destination_factory : AutoMapperSpecBase
{
    class Source
    {
    }
    class Destination
    {
    }
    class Factory : IDestinationFactory<Source, Destination>
    {
        public Destination Create(Source source, ResolutionContext context) => new();
    }
    protected override MapperConfiguration CreateConfiguration() => new(cfg =>
    {
        cfg.ConstructServicesUsing(type => null);
        cfg.CreateMap<Source, Destination>().ConstructUsing<Factory>();
    });
    [Fact]
    public void Should_say_which_type_could_not_be_created() =>
        new Action(() => Mapper.Map<Destination>(new Source())).ShouldThrow<AutoMapperMappingException>().Message.ShouldBe("Cannot create an instance of type " + typeof(Factory));
}

public class When_a_destination_factory_is_registered_as_an_interface : AutoMapperSpecBase
{
    class Source
    {
    }
    class Destination(string createdBy)
    {
        public string CreatedBy { get; } = createdBy;
    }
    class Factory : IDestinationFactory<Source, Destination>
    {
        public Destination Create(Source source, ResolutionContext context) => new("implementation");
    }
    readonly List<Type> _requested = [];
    protected override MapperConfiguration CreateConfiguration() => new(cfg =>
    {
        cfg.ConstructServicesUsing(type =>
        {
            _requested.Add(type);
            return new Factory();
        });
        cfg.CreateMap<Source, Destination>().ConstructUsing<IDestinationFactory<Source, Destination>>();
    });
    [Fact]
    public void Should_ask_the_service_constructor_for_the_interface()
    {
        Mapper.Map<Destination>(new Source()).CreatedBy.ShouldBe("implementation");
        _requested.ShouldBe([typeof(IDestinationFactory<Source, Destination>)]);
    }
    [Fact]
    public void Should_work_with_the_type_overload()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.ConstructServicesUsing(type => type == typeof(IDestinationFactory<Source, Destination>) ? new Factory() : null);
            cfg.CreateMap<Source, Destination>().ConstructUsing(typeof(IDestinationFactory<Source, Destination>));
        });
        configuration.CreateMapper().Map<Destination>(new Source()).CreatedBy.ShouldBe("implementation");
    }
}

public class When_a_map_has_several_ConstructUsing_calls
{
    class Source
    {
    }
    class Destination(string createdBy)
    {
        public string CreatedBy { get; } = createdBy;
    }
    class Factory : IDestinationFactory<Source, Destination>
    {
        public Destination Create(Source source, ResolutionContext context) => new("factory");
    }
    class OtherFactory : IDestinationFactory<Source, Destination>
    {
        public Destination Create(Source source, ResolutionContext context) => new("other factory");
    }
    static string CreatedBy(Action<IMappingExpression<Source, Destination>> configure)
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.ConstructServicesUsing(type => type == typeof(Destination) ? new Destination("service locator") : Activator.CreateInstance(type));
            configure(cfg.CreateMap<Source, Destination>());
        });
        configuration.AssertConfigurationIsValid();
        return configuration.CreateMapper().Map<Destination>(new Source()).CreatedBy;
    }
    [Fact]
    public void A_factory_replaces_what_was_configured_before_it()
    {
        CreatedBy(map => map.ConstructUsing((src, context) => new Destination("function")).ConstructUsing<Factory>()).ShouldBe("factory");
        CreatedBy(map => map.ConstructUsing(src => new Destination("expression")).ConstructUsing(typeof(Factory))).ShouldBe("factory");
        CreatedBy(map => map.ConstructUsingServiceLocator().ConstructUsing<Factory>()).ShouldBe("factory");
        CreatedBy(map => map.ConstructUsing<OtherFactory>().ConstructUsing<Factory>()).ShouldBe("factory");
        CreatedBy(map => map.ConstructUsing(typeof(Factory)).ConstructUsing<OtherFactory>()).ShouldBe("other factory");
    }
    [Fact]
    public void A_factory_is_replaced_by_what_is_configured_after_it()
    {
        CreatedBy(map => map.ConstructUsing<Factory>().ConstructUsing((src, context) => new Destination("function"))).ShouldBe("function");
        CreatedBy(map => map.ConstructUsing(typeof(Factory)).ConstructUsing(src => new Destination("expression"))).ShouldBe("expression");
        CreatedBy(map => map.ConstructUsing<Factory>().ConstructUsingServiceLocator()).ShouldBe("service locator");
    }
}

public class When_a_destination_factory_replaces_constructor_mapping : AutoMapperSpecBase
{
    class Source
    {
        public int Value { get; set; }
        public string Name { get; set; }
    }
    class Destination
    {
        // Constructor mapping would use this constructor.
        public Destination(int value) => ConstructedWith = Value = value;
        public int ConstructedWith { get; }
        public int Value { get; set; }
        public string Name { get; set; }
    }
    class Factory : IDestinationFactory<Source, Destination>
    {
        public Destination Create(Source source, ResolutionContext context) => new(-1);
    }
    protected override MapperConfiguration CreateConfiguration() => new(cfg => cfg.CreateMap<Source, Destination>().ConstructUsing<Factory>());
    [Fact]
    public void Should_not_map_the_constructor() => FindTypeMapFor<Source, Destination>().ConstructorMap.ShouldBeNull();
    [Fact]
    public void Should_map_all_members_onto_the_created_object()
    {
        var destination = Mapper.Map<Destination>(new Source { Value = 42, Name = "name" });
        destination.ConstructedWith.ShouldBe(-1);
        destination.Value.ShouldBe(42);
        destination.Name.ShouldBe("name");
    }
}

public class When_a_map_with_a_destination_factory_has_before_and_after_map_actions
{
    class Source
    {
        public int Value { get; set; }
    }
    class Destination
    {
        public int Value { get; set; }
    }
    class Factory(List<string> log) : IDestinationFactory<Source, Destination>
    {
        public Destination Create(Source source, ResolutionContext context)
        {
            log.Add("create");
            return new();
        }
    }
    static List<string> Map(Action<IMappingExpression<Source, Destination>, List<string>> constructUsing)
    {
        List<string> log = [];
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.ConstructServicesUsing(type => new Factory(log));
            constructUsing(cfg.CreateMap<Source, Destination>()
                .BeforeMap((src, dest) => log.Add("before map, value " + dest.Value))
                .AfterMap((src, dest) => log.Add("after map, value " + dest.Value)), log);
        });
        configuration.CreateMapper().Map<Destination>(new Source { Value = 42 });
        return log;
    }
    [Fact]
    public void They_get_the_created_object_as_with_the_function_overload()
    {
        var withFactory = Map((map, log) => map.ConstructUsing<Factory>());
        withFactory.ShouldBe(["create", "before map, value 0", "after map, value 42"]);
        Map((map, log) => map.ConstructUsing((src, context) =>
        {
            log.Add("create");
            return new Destination();
        })).ShouldBe(withFactory);
    }
}

public class When_a_map_with_a_destination_factory_preserves_references
{
    class Source
    {
        public int Id { get; set; }
        public Source Parent { get; set; }
        public Source Favorite { get; set; }
        public List<Source> Children { get; set; } = [];
    }
    class Destination
    {
        public int Id { get; set; }
        public Destination Parent { get; set; }
        public Destination Favorite { get; set; }
        public List<Destination> Children { get; set; }
    }
    class Factory(List<int> created) : IDestinationFactory<Source, Destination>
    {
        public Destination Create(Source source, ResolutionContext context)
        {
            created.Add(source.Id);
            return new();
        }
    }
    static (Destination Destination, List<int> Created) Map(Action<IMappingExpression<Source, Destination>, List<int>> constructUsing)
    {
        List<int> created = [];
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.ConstructServicesUsing(type => new Factory(created));
            constructUsing(cfg.CreateMap<Source, Destination>().PreserveReferences(), created);
        });
        var root = new Source { Id = 1 };
        root.Children.Add(new() { Id = 2, Parent = root });
        root.Children.Add(new() { Id = 3, Parent = root });
        root.Favorite = root.Children[1];
        return (configuration.CreateMapper().Map<Destination>(root), created);
    }
    [Fact]
    public void A_source_object_gets_one_destination_object_as_with_the_function_overload()
    {
        var (destination, created) = Map((map, _) => map.ConstructUsing<Factory>());
        destination.Children.Select(child => child.Id).ShouldBe([2, 3]);
        destination.Children.ShouldAllBe(child => child.Parent == destination);
        destination.Favorite.ShouldBeSameAs(destination.Children[1]);
        created.Order().ShouldBe([1, 2, 3]);
        Map((map, log) => map.ConstructUsing((src, context) =>
        {
            log.Add(src.Id);
            return new Destination();
        })).Created.ShouldBe(created);
    }
}

public class When_a_map_with_a_destination_factory_has_a_max_depth
{
    class Source
    {
        public int Level { get; set; }
        public Source Child { get; set; }
    }
    class Destination
    {
        public int Level { get; set; }
        public Destination Child { get; set; }
    }
    class Factory(List<int> created) : IDestinationFactory<Source, Destination>
    {
        public Destination Create(Source source, ResolutionContext context)
        {
            created.Add(source.Level);
            return new();
        }
    }
    static (List<int> Mapped, List<int> Created) Map(Action<IMappingExpression<Source, Destination>, List<int>> constructUsing)
    {
        List<int> created = [];
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.ConstructServicesUsing(type => new Factory(created));
            constructUsing(cfg.CreateMap<Source, Destination>().MaxDepth(3), created);
        });
        Source source = null;
        for (var level = 6; level > 0; level--)
        {
            source = new() { Level = level, Child = source };
        }
        List<int> mapped = [];
        for (var destination = configuration.CreateMapper().Map<Destination>(source); destination != null; destination = destination.Child)
        {
            mapped.Add(destination.Level);
        }
        return (mapped, created);
    }
    [Fact]
    public void The_factory_is_called_for_the_levels_that_are_mapped_as_with_the_function_overload()
    {
        var (mapped, created) = Map((map, _) => map.ConstructUsing<Factory>());
        mapped.ShouldBe([1, 2, 3]);
        created.ShouldBe(mapped);
        var withFunction = Map((map, log) => map.ConstructUsing((src, context) =>
        {
            log.Add(src.Level);
            return new Destination();
        }));
        withFunction.Mapped.ShouldBe(mapped);
        withFunction.Created.ShouldBe(created);
    }
}

public class When_validating_a_map_with_a_destination_factory
{
    class Source
    {
        public int Value { get; set; }
    }
    class Destination(string createdBy)
    {
        public string CreatedBy { get; } = createdBy;
        public int Value { get; set; }
    }
    class DestinationWithMore(string createdBy) : Destination(createdBy)
    {
        public int More { get; set; }
    }
    class Factory : IDestinationFactory<Source, DestinationWithMore>
    {
        public DestinationWithMore Create(Source source, ResolutionContext context) => new("factory");
    }
    [Fact]
    public void The_destination_needs_no_constructor_that_can_be_mapped() =>
        new MapperConfiguration(cfg => cfg.CreateMap<Source, Destination>().ConstructUsing<Factory>()).AssertConfigurationIsValid();
    [Fact]
    public void Without_the_factory_the_same_map_is_not_valid() =>
        new Action(new MapperConfiguration(cfg => cfg.CreateMap<Source, Destination>()).AssertConfigurationIsValid)
            .ShouldThrow<AutoMapperConfigurationException>().Errors.Single().CanConstruct.ShouldBeFalse();
    [Fact]
    public void Unmapped_members_are_still_reported()
    {
        var error = new Action(new MapperConfiguration(cfg => cfg.CreateMap<Source, DestinationWithMore>().ConstructUsing<Factory>()).AssertConfigurationIsValid)
            .ShouldThrow<AutoMapperConfigurationException>().Errors.Single();
        error.CanConstruct.ShouldBeTrue();
        error.UnmappedPropertyNames.ShouldBe(["More"]);
    }
}

public class When_a_map_with_a_destination_factory_is_inherited
{
    class Source
    {
        public int Value { get; set; }
    }
    class DerivedSource : Source
    {
    }
    class Destination
    {
        string _createdBy = "constructor";
        public string CreatedBy() => _createdBy;
        public Destination By(string createdBy)
        {
            _createdBy = createdBy;
            return this;
        }
        public int Value { get; set; }
    }
    class DerivedDestination : Destination
    {
    }
    class Factory : IDestinationFactory<Source, Destination>
    {
        public Destination Create(Source source, ResolutionContext context) => new Destination().By("factory for " + source.GetType().Name);
    }
    class DerivedFactory : IDestinationFactory<DerivedSource, DerivedDestination>
    {
        public DerivedDestination Create(DerivedSource source, ResolutionContext context) => (DerivedDestination)new DerivedDestination().By("derived factory");
    }
    // Every fact is checked for a factory and for the function overload: derived maps have to treat the two the same way.
    static readonly Action<IMappingExpression<Source, Destination>>[] FactoryAndFunction =
    [
        map => map.ConstructUsing<Factory>(),
        map => map.ConstructUsing((src, context) => new Destination().By("factory for " + src.GetType().Name)),
    ];
    static Destination Map(Action<IMapperConfigurationExpression> configure, Source source)
    {
        var configuration = new MapperConfiguration(configure);
        configuration.AssertConfigurationIsValid();
        var destination = configuration.CreateMapper().Map<Destination>(source);
        destination.Value.ShouldBe(source.Value);
        return destination;
    }
    [Fact]
    public void A_derived_source_mapped_with_the_base_map_is_given_to_the_factory()
    {
        foreach (var constructUsing in FactoryAndFunction)
        {
            var destination = Map(cfg => constructUsing(cfg.CreateMap<Source, Destination>()), new DerivedSource { Value = 42 });
            destination.ShouldBeOfType<Destination>().CreatedBy().ShouldBe("factory for DerivedSource");
        }
    }
    [Fact]
    public void An_included_derived_map_does_not_inherit_it()
    {
        foreach (var constructUsing in FactoryAndFunction)
        {
            Action<IMapperConfigurationExpression> configure = cfg =>
            {
                constructUsing(cfg.CreateMap<Source, Destination>().Include<DerivedSource, DerivedDestination>());
                cfg.CreateMap<DerivedSource, DerivedDestination>();
            };
            Map(configure, new DerivedSource { Value = 42 }).ShouldBeOfType<DerivedDestination>().CreatedBy().ShouldBe("constructor");
            Map(configure, new Source { Value = 42 }).ShouldBeOfType<Destination>().CreatedBy().ShouldBe("factory for Source");
        }
    }
    [Fact]
    public void A_derived_map_that_includes_the_base_map_does_not_inherit_it()
    {
        foreach (var constructUsing in FactoryAndFunction)
        {
            Action<IMapperConfigurationExpression> configure = cfg =>
            {
                constructUsing(cfg.CreateMap<Source, Destination>());
                cfg.CreateMap<DerivedSource, DerivedDestination>().IncludeBase<Source, Destination>();
            };
            Map(configure, new DerivedSource { Value = 42 }).ShouldBeOfType<DerivedDestination>().CreatedBy().ShouldBe("constructor");
        }
    }
    [Fact]
    public void IncludeAllDerived_does_not_pass_it_on()
    {
        foreach (var constructUsing in FactoryAndFunction)
        {
            Action<IMapperConfigurationExpression> configure = cfg =>
            {
                constructUsing(cfg.CreateMap<Source, Destination>().IncludeAllDerived());
                cfg.CreateMap<DerivedSource, DerivedDestination>();
            };
            Map(configure, new DerivedSource { Value = 42 }).ShouldBeOfType<DerivedDestination>().CreatedBy().ShouldBe("constructor");
        }
    }
    [Fact]
    public void A_derived_map_can_have_its_own_factory()
    {
        Action<IMapperConfigurationExpression> configure = cfg =>
        {
            cfg.CreateMap<Source, Destination>().ConstructUsing<Factory>().Include<DerivedSource, DerivedDestination>();
            cfg.CreateMap<DerivedSource, DerivedDestination>().ConstructUsing<DerivedFactory>();
        };
        Map(configure, new DerivedSource { Value = 42 }).ShouldBeOfType<DerivedDestination>().CreatedBy().ShouldBe("derived factory");
        Map(configure, new Source { Value = 42 }).ShouldBeOfType<Destination>().CreatedBy().ShouldBe("factory for Source");
    }
}

public class When_a_destination_factory_is_declared_for_a_base_source_and_a_derived_destination : AutoMapperSpecBase
{
    class BaseSource
    {
        public int Value { get; set; }
    }
    class Source : BaseSource
    {
    }
    class Destination
    {
        public int Value { get; set; }
    }
    class DerivedDestination : Destination
    {
    }
    interface IOtherDestination
    {
        int Value { get; set; }
    }
    class OtherDestination : IOtherDestination
    {
        public int Value { get; set; }
    }
    class Factory : IDestinationFactory<BaseSource, DerivedDestination>, IDestinationFactory<object, OtherDestination>
    {
        public DerivedDestination Create(BaseSource source, ResolutionContext context) => new();
        public OtherDestination Create(object source, ResolutionContext context) => new();
    }
    protected override MapperConfiguration CreateConfiguration() => new(cfg =>
    {
        // The constraint of the generic overload is satisfied through the variance of the interface.
        cfg.CreateMap<Source, Destination>().ConstructUsing<Factory>();
        cfg.CreateMap<BaseSource, Destination>().ConstructUsing(typeof(Factory));
        cfg.CreateMap<Source, IOtherDestination>().ConstructUsing<Factory>();
        cfg.CreateMap<BaseSource, IOtherDestination>().ConstructUsing(typeof(Factory));
    });
    [Fact]
    public void Should_be_accepted_by_the_generic_overload()
    {
        Mapper.Map<Destination>(new Source { Value = 42 }).ShouldBeOfType<DerivedDestination>().Value.ShouldBe(42);
        Mapper.Map<IOtherDestination>(new Source { Value = 42 }).ShouldBeOfType<OtherDestination>().Value.ShouldBe(42);
    }
    [Fact]
    public void Should_be_accepted_by_the_type_overload()
    {
        Mapper.Map<BaseSource, Destination>(new BaseSource { Value = 42 }).ShouldBeOfType<DerivedDestination>().Value.ShouldBe(42);
        Mapper.Map<BaseSource, IOtherDestination>(new BaseSource { Value = 42 }).ShouldBeOfType<OtherDestination>().Value.ShouldBe(42);
    }
}

public class When_a_destination_factory_is_used_with_value_types : AutoMapperSpecBase
{
    struct Source
    {
        public int Value { get; set; }
    }
    class Destination(string createdBy)
    {
        public string CreatedBy { get; } = createdBy;
        public int Value { get; set; }
    }
    struct StructDestination
    {
        public string CreatedBy;
        public int Value { get; set; }
    }
    class ClassSource
    {
        public int Value { get; set; }
    }
    // Variance does not cover value types: the type overload boxes and unboxes as needed.
    class AnySourceFactory : IDestinationFactory<object, Destination>
    {
        public Destination Create(object source, ResolutionContext context) => new("factory for " + source.GetType().Name);
    }
    class StructFactory : IDestinationFactory<Source, StructDestination>
    {
        public StructDestination Create(Source source, ResolutionContext context) => new() { CreatedBy = "struct factory" };
    }
    class ObjectFactory : IDestinationFactory<object, object>
    {
        public object Create(object source, ResolutionContext context) => new StructDestination { CreatedBy = "object factory" };
    }
    protected override MapperConfiguration CreateConfiguration() => new(cfg =>
    {
        cfg.CreateMap<Source, Destination>().ConstructUsing(typeof(AnySourceFactory));
        cfg.CreateMap<Source, StructDestination>().ConstructUsing<StructFactory>().ForMember(d => d.CreatedBy, o => o.Ignore());
        cfg.CreateMap(typeof(ClassSource), typeof(StructDestination)).ConstructUsing<ObjectFactory>().ForMember(nameof(StructDestination.CreatedBy), o => o.Ignore());
    });
    [Fact]
    public void A_struct_source_can_be_given_to_a_factory_for_object()
    {
        var destination = Mapper.Map<Destination>(new Source { Value = 42 });
        destination.CreatedBy.ShouldBe("factory for Source");
        destination.Value.ShouldBe(42);
    }
    [Fact]
    public void A_factory_can_create_a_struct()
    {
        var destination = Mapper.Map<StructDestination>(new Source { Value = 42 });
        destination.CreatedBy.ShouldBe("struct factory");
        destination.Value.ShouldBe(42);
    }
    [Fact]
    public void A_factory_for_object_can_create_a_struct_for_a_map_created_from_types()
    {
        var destination = Mapper.Map<StructDestination>(new ClassSource { Value = 42 });
        destination.CreatedBy.ShouldBe("object factory");
        destination.Value.ShouldBe(42);
    }
    [Fact]
    public void A_struct_the_caller_passes_is_replaced_by_the_one_the_factory_creates_as_with_the_function_overload()
    {
        var existing = new StructDestination { CreatedBy = "caller" };
        Mapper.Map(new Source { Value = 42 }, existing).CreatedBy.ShouldBe("struct factory");
        var withFunction = new MapperConfiguration(cfg => cfg.CreateMap<Source, StructDestination>()
            .ConstructUsing((src, context) => new StructDestination { CreatedBy = "function" }).ForMember(d => d.CreatedBy, o => o.Ignore()));
        withFunction.CreateMapper().Map(new Source { Value = 42 }, existing).CreatedBy.ShouldBe("function");
    }
}

public class When_the_type_passed_to_ConstructUsing_cannot_create_the_destination
{
    class Source
    {
    }
    class DerivedSource : Source
    {
    }
    class Destination
    {
    }
    class DerivedDestination : Destination
    {
    }
    class NotAFactory
    {
    }
    class Factory : IDestinationFactory<Source, Destination>
    {
        public Destination Create(Source source, ResolutionContext context) => new();
    }
    class DerivedSourceFactory : IDestinationFactory<DerivedSource, Destination>
    {
        public Destination Create(DerivedSource source, ResolutionContext context) => new();
    }
    class Factory<T> : IDestinationFactory<Source, Destination>
    {
        public Destination Create(Source source, ResolutionContext context) => new();
    }
    static void ShouldNotBeAccepted<TSource, TDestination>(Type factoryType) =>
        new Action(() => new MapperConfiguration(cfg => cfg.CreateMap<TSource, TDestination>().ConstructUsing(factoryType))).ShouldThrow<AutoMapperConfigurationException>().Message.ShouldBe(
            $"{factoryType} cannot create the destination of the map from {typeof(TSource)} to {typeof(TDestination)}. It has to implement IDestinationFactory<TSource, TDestination> " +
            $"with a TSource that {typeof(TSource)} can be assigned to and a TDestination that can be assigned to {typeof(TDestination)}.");
    [Fact]
    public void A_type_that_is_not_a_factory_fails_when_the_configuration_is_built() => ShouldNotBeAccepted<Source, Destination>(typeof(NotAFactory));
    [Fact]
    public void A_factory_that_cannot_take_the_source_fails_when_the_configuration_is_built() => ShouldNotBeAccepted<Source, Destination>(typeof(DerivedSourceFactory));
    [Fact]
    public void A_factory_that_returns_a_base_type_of_the_destination_fails_when_the_configuration_is_built() => ShouldNotBeAccepted<Source, DerivedDestination>(typeof(Factory));
    [Fact]
    public void An_open_generic_factory_for_a_map_that_is_not_generic_fails_when_the_configuration_is_built() =>
        new Action(() => new MapperConfiguration(cfg => cfg.CreateMap<Source, Destination>().ConstructUsing(typeof(Factory<>)))).ShouldThrow<AutoMapperConfigurationException>()
            .Message.ShouldBe($"Cannot close the open generic destination factory {typeof(Factory<>)} for the map from {typeof(Source)} to {typeof(Destination)}.");
    [Fact]
    public void A_map_created_from_types_fails_the_same_way() =>
        new Action(() => new MapperConfiguration(cfg => cfg.CreateMap(typeof(Source), typeof(Destination)).ConstructUsing(typeof(NotAFactory))))
            .ShouldThrow<AutoMapperConfigurationException>().Message.ShouldStartWith($"{typeof(NotAFactory)} cannot create the destination of the map from {typeof(Source)} to {typeof(Destination)}.");
    [Fact]
    public void A_type_that_is_not_a_factory_fails_when_the_configuration_is_built_for_an_open_generic_map_too() =>
        new Action(() => new MapperConfiguration(cfg => cfg.CreateMap(typeof(List<>), typeof(Destination)).ConstructUsing(typeof(NotAFactory))))
            .ShouldThrow<AutoMapperConfigurationException>().Message.ShouldStartWith($"{typeof(NotAFactory)} cannot create the destination of the map from {typeof(List<>)} to {typeof(Destination)}.");
    [Fact]
    public void Null_is_rejected_at_once() =>
        new Action(() => new MapperConfiguration(cfg => cfg.CreateMap<Source, Destination>().ConstructUsing((Type)null))).ShouldThrow<ArgumentNullException>()
            .ParamName.ShouldBe("destinationFactoryType");
}

public class When_an_open_generic_map_has_an_open_generic_destination_factory : AutoMapperSpecBase
{
    class Source<T>
    {
        public T Value { get; set; }
    }
    class Source
    {
        public int Value { get; set; }
    }
    // No constructor the mapper could use, so validating the open maps shows that they need none.
    class Destination<T>(string createdBy)
    {
        public string CreatedBy { get; } = createdBy;
        public T Value { get; set; }
    }
    class Destination(string createdBy)
    {
        public string CreatedBy { get; } = createdBy;
        public int Value { get; set; }
    }
    class OtherDestination<T>(string createdBy) : Destination<T>(createdBy);
    class Factory<T> : IDestinationFactory<Source<T>, Destination<T>>
    {
        public Destination<T> Create(Source<T> source, ResolutionContext context) => new("factory of " + typeof(T).Name);
    }
    class Factory<TSource, TDestination> : IDestinationFactory<Source<TSource>, OtherDestination<TDestination>>
    {
        public OtherDestination<TDestination> Create(Source<TSource> source, ResolutionContext context) => new($"factory of {typeof(TSource).Name} and {typeof(TDestination).Name}");
    }
    class FromGenericFactory<T> : IDestinationFactory<Source<T>, Destination>
    {
        public Destination Create(Source<T> source, ResolutionContext context) => new("factory from " + typeof(T).Name);
    }
    class ToGenericFactory<T> : IDestinationFactory<Source, Destination<T>>
    {
        public Destination<T> Create(Source source, ResolutionContext context) => new("factory to " + typeof(T).Name);
    }
    protected override MapperConfiguration CreateConfiguration() => new(cfg =>
    {
        cfg.CreateMap(typeof(Source<>), typeof(Destination<>)).ConstructUsing(typeof(Factory<>));
        cfg.CreateMap(typeof(Source<>), typeof(OtherDestination<>)).ConstructUsing(typeof(Factory<,>));
        cfg.CreateMap(typeof(Source<>), typeof(Destination)).ConstructUsing(typeof(FromGenericFactory<>));
        cfg.CreateMap(typeof(Source), typeof(Destination<>)).ConstructUsing(typeof(ToGenericFactory<>));
    });
    [Fact]
    public void Should_close_the_factory_for_each_closed_map()
    {
        var ofInt = Mapper.Map<Destination<int>>(new Source<int> { Value = 42 });
        ofInt.CreatedBy.ShouldBe("factory of Int32");
        ofInt.Value.ShouldBe(42);
        var ofString = Mapper.Map<Destination<string>>(new Source<string> { Value = "value" });
        ofString.CreatedBy.ShouldBe("factory of String");
        ofString.Value.ShouldBe("value");
    }
    [Fact]
    public void Should_close_the_factory_with_the_source_and_then_the_destination_type_arguments()
    {
        var destination = Mapper.Map<OtherDestination<long>>(new Source<int> { Value = 42 });
        destination.CreatedBy.ShouldBe("factory of Int32 and Int64");
        destination.Value.ShouldBe(42);
    }
    [Fact]
    public void Should_close_the_factory_when_only_the_source_is_generic()
    {
        var destination = Mapper.Map<Destination>(new Source<int> { Value = 42 });
        destination.CreatedBy.ShouldBe("factory from Int32");
        destination.Value.ShouldBe(42);
    }
    [Fact]
    public void Should_close_the_factory_when_only_the_destination_is_generic()
    {
        var destination = Mapper.Map<Destination<long>>(new Source { Value = 42 });
        destination.CreatedBy.ShouldBe("factory to Int64");
        destination.Value.ShouldBe(42);
    }
}

public class When_only_the_destination_of_a_generic_map_with_a_destination_factory_is_open : AutoMapperSpecBase
{
    class Source<T>
    {
        public T Value { get; set; }
    }
    class Destination<T>(string createdBy)
    {
        public string CreatedBy { get; } = createdBy;
        public T Value { get; set; }
    }
    class Factory<T> : IDestinationFactory<Source<int>, Destination<T>>
    {
        public Destination<T> Create(Source<int> source, ResolutionContext context) => new("factory to " + typeof(T).Name);
    }
    protected override MapperConfiguration CreateConfiguration() => new(cfg => cfg.CreateMap(typeof(Source<int>), typeof(Destination<>)).ConstructUsing(typeof(Factory<>)));
    [Fact]
    public void Should_close_the_factory_with_the_type_arguments_of_the_open_side_only()
    {
        var destination = Mapper.Map<Destination<long>>(new Source<int> { Value = 42 });
        destination.CreatedBy.ShouldBe("factory to Int64");
        destination.Value.ShouldBe(42);
    }
}

public class When_an_open_generic_map_has_a_destination_factory_that_is_not_generic : AutoMapperSpecBase
{
    class Source<T>
    {
        public T Value { get; set; }
    }
    class Destination(string createdBy)
    {
        public string CreatedBy { get; } = createdBy;
        public int Value { get; set; }
    }
    class OtherDestination(string createdBy) : Destination(createdBy);
    class Factory : IDestinationFactory<object, Destination>
    {
        public Destination Create(object source, ResolutionContext context) => new("factory for " + source.GetType().GenericTypeArguments[0].Name);
    }
    class ObjectFactory : IDestinationFactory<object, object>
    {
        public object Create(object source, ResolutionContext context) => new OtherDestination("object factory for " + source.GetType().GenericTypeArguments[0].Name);
    }
    protected override MapperConfiguration CreateConfiguration() => new(cfg =>
    {
        cfg.CreateMap(typeof(Source<>), typeof(Destination)).ConstructUsing(typeof(Factory));
        cfg.CreateMap(typeof(Source<>), typeof(OtherDestination)).ConstructUsing<ObjectFactory>();
    });
    [Fact]
    public void Should_use_the_factory_for_every_closed_map()
    {
        Mapper.Map<Destination>(new Source<int> { Value = 42 }).CreatedBy.ShouldBe("factory for Int32");
        Mapper.Map<Destination>(new Source<long> { Value = 42 }).CreatedBy.ShouldBe("factory for Int64");
    }
    [Fact]
    public void Should_work_with_the_generic_overload()
    {
        var destination = Mapper.Map<OtherDestination>(new Source<int> { Value = 42 });
        destination.CreatedBy.ShouldBe("object factory for Int32");
        destination.Value.ShouldBe(42);
    }
}

public class When_an_open_generic_destination_factory_does_not_fit_a_closed_map : AutoMapperSpecBase
{
    class Source<T>
    {
        public T Value { get; set; }
    }
    class Destination<T>
    {
        public T Value { get; set; }
    }
    class OtherDestination<T>
    {
        public T Value { get; set; }
    }
    class ClassesOnlyFactory<T> : IDestinationFactory<Source<T>, Destination<T>> where T : class
    {
        public Destination<T> Create(Source<T> source, ResolutionContext context) => new();
    }
    class WrongDestinationFactory<T> : IDestinationFactory<Source<T>, Destination<T>>
    {
        public Destination<T> Create(Source<T> source, ResolutionContext context) => new();
    }
    protected override MapperConfiguration CreateConfiguration() => new(cfg =>
    {
        cfg.CreateMap(typeof(Source<>), typeof(Destination<>)).ConstructUsing(typeof(ClassesOnlyFactory<>));
        cfg.CreateMap(typeof(Source<>), typeof(OtherDestination<>)).ConstructUsing(typeof(WrongDestinationFactory<>));
    });
    [Fact]
    public void Should_map_the_closed_types_the_factory_fits() => Mapper.Map<Destination<string>>(new Source<string> { Value = "value" }).Value.ShouldBe("value");
    [Fact]
    public void Should_fail_when_the_closed_map_is_first_used_if_the_factory_cannot_be_closed()
    {
        var exception = new Action(() => Mapper.Map<Destination<int>>(new Source<int>())).ShouldThrow<AutoMapperConfigurationException>();
        exception.Message.ShouldBe(
            $"Cannot close the open generic destination factory {typeof(ClassesOnlyFactory<>)} for the map from {typeof(Source<int>)} to {typeof(Destination<int>)}.");
        exception.InnerException.ShouldBeOfType<ArgumentException>();
    }
    [Fact]
    public void Should_fail_when_the_closed_map_is_first_used_if_the_closed_factory_is_for_other_types() =>
        new Action(() => Mapper.Map<OtherDestination<int>>(new Source<int>())).ShouldThrow<AutoMapperConfigurationException>().Message.ShouldStartWith(
            $"{typeof(WrongDestinationFactory<int>)} cannot create the destination of the map from {typeof(Source<int>)} to {typeof(OtherDestination<int>)}.");
}

public class When_projecting_with_a_map_that_has_a_destination_factory
{
    class Source
    {
        public int Value { get; set; }
    }
    class Destination
    {
        string _createdBy = "constructor";
        public string CreatedBy() => _createdBy;
        public Destination By(string createdBy)
        {
            _createdBy = createdBy;
            return this;
        }
        public int Value { get; set; }
    }
    class NoDefaultConstructor(string createdBy)
    {
        public string CreatedBy { get; } = createdBy;
        public int Value { get; set; }
    }
    class Factory : IDestinationFactory<Source, Destination>, IDestinationFactory<Source, NoDefaultConstructor>
    {
        public Destination Create(Source source, ResolutionContext context) => new Destination().By("factory");
        NoDefaultConstructor IDestinationFactory<Source, NoDefaultConstructor>.Create(Source source, ResolutionContext context) => new("factory");
    }
    static readonly IQueryable<Source> Sources = new[] { new Source { Value = 42 } }.AsQueryable();
    [Fact]
    public void ProjectTo_does_not_use_the_factory_as_it_does_not_use_the_function_overload()
    {
        var withFactory = new MapperConfiguration(cfg => cfg.CreateMap<Source, Destination>().ConstructUsing<Factory>());
        var withFunction = new MapperConfiguration(cfg => cfg.CreateMap<Source, Destination>().ConstructUsing((src, context) => new Destination().By("function")));
        foreach (var configuration in new[] { withFactory, withFunction })
        {
            var destination = Sources.ProjectTo<Destination>(configuration).Single();
            destination.CreatedBy().ShouldBe("constructor");
            destination.Value.ShouldBe(42);
        }
        withFactory.CreateMapper().Map<Destination>(Sources.Single()).CreatedBy().ShouldBe("factory");
    }
    [Fact]
    public void ProjectTo_needs_a_parameterless_constructor_as_with_the_function_overload()
    {
        var withFactory = new MapperConfiguration(cfg => cfg.CreateMap<Source, NoDefaultConstructor>().ConstructUsing<Factory>());
        var withFunction = new MapperConfiguration(cfg => cfg.CreateMap<Source, NoDefaultConstructor>().ConstructUsing((src, context) => new NoDefaultConstructor("function")));
        // The ArgumentException about the missing constructor arrives inside the exception that names the map.
        var error = new Action(() => Sources.ProjectTo<NoDefaultConstructor>(withFactory).Single()).ShouldThrow<AutoMapperMappingException>();
        error.InnerException.ShouldBeOfType<ArgumentException>();
        error.TypeMap.DestinationType.ShouldBe(typeof(NoDefaultConstructor));
        new Action(() => Sources.ProjectTo<NoDefaultConstructor>(withFunction).Single()).ShouldThrow<AutoMapperMappingException>()
            .InnerException.Message.ShouldBe(error.InnerException.Message);
    }
}

public class When_a_member_is_mapped_with_a_map_that_has_a_destination_factory : AutoMapperSpecBase
{
    class Source
    {
        public InnerSource Inner { get; set; }
        public List<InnerSource> Inners { get; set; }
    }
    class InnerSource
    {
        public int Value { get; set; }
    }
    class Destination
    {
        public InnerDestination Inner { get; set; }
        public InnerDestination[] Inners { get; set; }
    }
    class InnerDestination(string createdBy)
    {
        public string CreatedBy { get; } = createdBy;
        public int Value { get; set; }
    }
    class Factory : IDestinationFactory<InnerSource, InnerDestination>
    {
        public InnerDestination Create(InnerSource source, ResolutionContext context) => new("factory for " + source.Value);
    }
    protected override MapperConfiguration CreateConfiguration() => new(cfg =>
    {
        cfg.CreateMap<Source, Destination>();
        cfg.CreateMap<InnerSource, InnerDestination>().ConstructUsing<Factory>();
    });
    [Fact]
    public void Should_create_the_member_and_the_elements_with_the_factory()
    {
        var destination = Mapper.Map<Destination>(new Source { Inner = new() { Value = 1 }, Inners = [new() { Value = 2 }, new() { Value = 3 }] });
        destination.Inner.CreatedBy.ShouldBe("factory for 1");
        destination.Inner.Value.ShouldBe(1);
        destination.Inners.Select(inner => inner.CreatedBy).ShouldBe(["factory for 2", "factory for 3"]);
        destination.Inners.Select(inner => inner.Value).ShouldBe([2, 3]);
    }
    [Fact]
    public void Should_not_call_the_factory_for_a_member_that_already_has_a_value()
    {
        var destination = new Destination { Inner = new("caller") };
        Mapper.Map(new Source { Inner = new() { Value = 1 } }, destination);
        destination.Inner.CreatedBy.ShouldBe("caller");
        destination.Inner.Value.ShouldBe(1);
    }
}

public class When_a_destination_factory_returns_null : AutoMapperSpecBase
{
    class Source
    {
        public int Value { get; set; }
    }
    class Destination
    {
        public int Value { get; set; }
    }
    class Factory : IDestinationFactory<Source, Destination>
    {
        public Destination Create(Source source, ResolutionContext context) => null;
    }
    protected override MapperConfiguration CreateConfiguration() => new(cfg => cfg.CreateMap<Source, Destination>().ConstructUsing<Factory>());
    [Fact]
    public void Should_fail_as_with_the_function_overload() =>
        new Action(() => Mapper.Map<Destination>(new Source())).ShouldThrow<AutoMapperMappingException>().InnerException.ShouldBeOfType<NullReferenceException>();
}

public class When_all_maps_get_a_destination_factory : AutoMapperSpecBase
{
    class Source
    {
        public int Value { get; set; }
        public InnerSource Inner { get; set; }
    }
    class InnerSource
    {
        public int Value { get; set; }
    }
    class Entity
    {
        string _createdBy = "constructor";
        public string CreatedBy() => _createdBy;
        public void By(string createdBy) => _createdBy = createdBy;
    }
    class Destination : Entity
    {
        public int Value { get; set; }
        public InnerDestination Inner { get; set; }
    }
    class InnerDestination : Entity
    {
        public int Value { get; set; }
    }
    class EntityFactory<TEntity> : IDestinationFactory<object, TEntity> where TEntity : Entity, new()
    {
        public TEntity Create(object source, ResolutionContext context)
        {
            TEntity entity = new();
            entity.By("factory of " + typeof(TEntity).Name);
            return entity;
        }
    }
    protected override MapperConfiguration CreateConfiguration() => new(cfg =>
    {
        cfg.CreateMap<Source, Destination>();
        cfg.CreateMap<InnerSource, InnerDestination>();
        cfg.ForAllMaps((typeMap, map) => map.ConstructUsing(typeof(EntityFactory<>).MakeGenericType(typeMap.DestinationType)));
    });
    [Fact]
    public void Should_create_every_destination_with_its_factory()
    {
        var destination = Mapper.Map<Destination>(new Source { Value = 1, Inner = new() { Value = 2 } });
        destination.CreatedBy().ShouldBe("factory of Destination");
        destination.Value.ShouldBe(1);
        destination.Inner.CreatedBy().ShouldBe("factory of InnerDestination");
        destination.Inner.Value.ShouldBe(2);
    }
}
