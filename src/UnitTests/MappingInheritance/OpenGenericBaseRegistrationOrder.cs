namespace ForkForward.Mapper.UnitTests.MappingInheritance;
public class OpenGenericBaseRegisteredAfterTheMapThatIncludesIt : AutoMapperSpecBase
{
    class FooBar { }
    class SourceBase<T> where T : class { }
    class SourceDerived : SourceBase<FooBar> { }
    class DestinationBase { }
    class DestinationDerived : DestinationBase { }
    protected override MapperConfiguration CreateConfiguration() => new(c =>
    {
        c.CreateMap<SourceDerived, DestinationDerived>().IncludeBase<SourceBase<FooBar>, DestinationBase>();
        c.CreateMap(typeof(SourceBase<>), typeof(DestinationBase));
    });
    [Fact]
    public void Should_work() => Map<DestinationDerived>(new SourceDerived()).ShouldBeOfType<DestinationDerived>();
}
/// <summary>
/// Every fact builds its configuration once for each order in which its maps can be registered, with the maps in the configuration itself
/// and then with each map in a profile of its own. The configuration has to be valid, hold the same type maps and map the same every time.
/// </summary>
public class OpenGenericBaseInEveryRegistrationOrder
{
    public class FooBar { }
    public class SourceBase<T> where T : class
    {
        public int BaseValue { get; set; }
        public string Other { get; set; }
    }
    public class SourceDerived : SourceBase<FooBar>
    {
        public int DerivedValue { get; set; }
    }
    public class SourceLeaf : SourceDerived
    {
        public int LeafValue { get; set; }
    }
    public interface ICountAfterMaps
    {
        int AfterMaps { get; set; }
    }
    public class DestinationBase : ICountAfterMaps
    {
        public int BaseValue { get; set; }
        public string Renamed { get; set; }
        public int AfterMaps { get; set; }
    }
    public class DestinationDerived : DestinationBase
    {
        public int DerivedValue { get; set; }
    }
    public class DestinationLeaf : DestinationDerived
    {
        public int LeafValue { get; set; }
    }
    public class DestinationBase<T> : ICountAfterMaps where T : class
    {
        public int BaseValue { get; set; }
        public string Renamed { get; set; }
        public int AfterMaps { get; set; }
    }
    public class GenericDestinationDerived : DestinationBase<FooBar>
    {
        public int DerivedValue { get; set; }
    }
    static IMappingExpression OpenBase(IProfileExpression c, Type destinationType) => c.CreateMap(typeof(SourceBase<>), destinationType)
        .ForMember(nameof(DestinationBase.Renamed), o => o.MapFrom(nameof(SourceBase<FooBar>.Other)))
        .ForMember(nameof(DestinationBase.AfterMaps), o => o.Ignore())
        .AfterMap((_, destination) => ((ICountAfterMaps)destination).AfterMaps++);
    static void OpenBase(IProfileExpression c) => OpenBase(c, typeof(DestinationBase));
    static void Derived(IProfileExpression c) => c.CreateMap<SourceDerived, DestinationDerived>().IncludeBase<SourceBase<FooBar>, DestinationBase>();
    static void ShouldMapTheDerivedType(IMapper mapper)
    {
        var destination = mapper.Map<DestinationDerived>(new SourceDerived { BaseValue = 1, Other = "other", DerivedValue = 2 });
        destination.BaseValue.ShouldBe(1);
        destination.Renamed.ShouldBe("other");
        destination.DerivedValue.ShouldBe(2);
        destination.AfterMaps.ShouldBe(1);
    }
    static void ShouldMapTheBaseType(IMapper mapper)
    {
        var destination = mapper.Map<DestinationBase>(new SourceBase<FooBar> { BaseValue = 3, Other = "base" });
        destination.ShouldBeOfType<DestinationBase>();
        destination.BaseValue.ShouldBe(3);
        destination.Renamed.ShouldBe("base");
        destination.AfterMaps.ShouldBe(1);
    }
    static void ShouldMapTheLeafType(IMapper mapper)
    {
        var destination = mapper.Map<DestinationLeaf>(new SourceLeaf { BaseValue = 1, Other = "other", DerivedValue = 2, LeafValue = 3 });
        destination.BaseValue.ShouldBe(1);
        destination.Renamed.ShouldBe("other");
        destination.DerivedValue.ShouldBe(2);
        destination.LeafValue.ShouldBe(3);
        destination.AfterMaps.ShouldBe(1);
    }
    [Fact]
    public void A_map_that_includes_the_closed_base_should_inherit_from_the_open_generic_map() => InEveryOrder([Derived, OpenBase], mapper =>
    {
        mapper.ConfigurationProvider.GetAllTypeMaps().Select(typeMap => typeMap.Types).ShouldBe(
            [new(typeof(SourceDerived), typeof(DestinationDerived)), new(typeof(SourceBase<>), typeof(DestinationBase))], ignoreOrder: true);
        ShouldMapTheDerivedType(mapper);
    });
    [Fact]
    public void Should_map_the_closed_base_type_before_the_derived_type() => InEveryOrder([Derived, OpenBase], mapper =>
    {
        ShouldMapTheBaseType(mapper);
        ShouldMapTheDerivedType(mapper);
        ShouldMapTheBaseType(mapper);
    });
    [Fact]
    public void Should_map_the_closed_base_type_after_the_derived_type() => InEveryOrder([Derived, OpenBase], mapper =>
    {
        ShouldMapTheDerivedType(mapper);
        ShouldMapTheBaseType(mapper);
    });
    [Fact]
    public void Should_configure_every_map_once()
    {
        Dictionary<TypePair, int> configured = [];
        InEveryOrder([Derived, OpenBase], mapper =>
        {
            configured.Keys.ShouldBe([new(typeof(SourceDerived), typeof(DestinationDerived)), new(typeof(SourceBase<>), typeof(DestinationBase)),
                new(typeof(SourceBase<FooBar>), typeof(DestinationBase))], ignoreOrder: true);
            configured.Values.ShouldAllBe(count => count == 1);
            ShouldMapTheDerivedType(mapper);
        }, c =>
        {
            configured.Clear();
            c.ForAllMaps((typeMap, _) => configured[typeMap.Types] = configured.GetValueOrDefault(typeMap.Types) + 1);
        });
    }
    [Fact]
    public void The_open_generic_map_can_include_the_derived_map() => InEveryOrder([
        c => c.CreateMap<SourceDerived, DestinationDerived>(),
        c => OpenBase(c, typeof(DestinationBase)).Include(typeof(SourceDerived), typeof(DestinationDerived))], mapper =>
    {
        ShouldMapTheDerivedType(mapper);
        ShouldMapTheBaseType(mapper);
    });
    [Fact]
    public void The_two_maps_can_include_each_other() => InEveryOrder([
        Derived,
        c => OpenBase(c, typeof(DestinationBase)).Include(typeof(SourceDerived), typeof(DestinationDerived))], mapper =>
    {
        ShouldMapTheDerivedType(mapper);
        ShouldMapTheBaseType(mapper);
    });
    [Fact]
    public void The_open_generic_map_can_include_all_derived_maps() => InEveryOrder([
        Derived,
        c => OpenBase(c, typeof(DestinationBase)).IncludeAllDerived()], mapper =>
    {
        ShouldMapTheDerivedType(mapper);
        ShouldMapTheBaseType(mapper);
    });
    [Fact]
    public void The_destination_can_be_an_open_generic_too() => InEveryOrder([
        c => c.CreateMap<SourceDerived, GenericDestinationDerived>().IncludeBase<SourceBase<FooBar>, DestinationBase<FooBar>>(),
        c => OpenBase(c, typeof(DestinationBase<>))], mapper =>
    {
        var derived = mapper.Map<GenericDestinationDerived>(new SourceDerived { BaseValue = 1, Other = "other", DerivedValue = 2 });
        derived.BaseValue.ShouldBe(1);
        derived.Renamed.ShouldBe("other");
        derived.DerivedValue.ShouldBe(2);
        derived.AfterMaps.ShouldBe(1);
        var closedBase = mapper.Map<DestinationBase<FooBar>>(new SourceBase<FooBar> { BaseValue = 3, Other = "base" });
        closedBase.ShouldBeOfType<DestinationBase<FooBar>>();
        closedBase.BaseValue.ShouldBe(3);
        closedBase.Renamed.ShouldBe("base");
        closedBase.AfterMaps.ShouldBe(1);
    });
    [Fact]
    public void The_generic_base_can_be_two_levels_up() => InEveryOrder([
        c => c.CreateMap<SourceLeaf, DestinationLeaf>().IncludeBase<SourceDerived, DestinationDerived>(),
        Derived,
        OpenBase], mapper =>
    {
        ShouldMapTheLeafType(mapper);
        ShouldMapTheDerivedType(mapper);
        ShouldMapTheBaseType(mapper);
    });
    [Fact]
    public void The_generic_base_can_be_two_levels_up_and_included_directly() => InEveryOrder([
        c => c.CreateMap<SourceLeaf, DestinationLeaf>().IncludeBase<SourceBase<FooBar>, DestinationBase>(),
        OpenBase], mapper =>
    {
        ShouldMapTheLeafType(mapper);
        ShouldMapTheBaseType(mapper);
    });
    [Fact]
    public void Should_project_the_derived_type() => InEveryOrder([Derived, OpenBase], mapper =>
    {
        var derived = mapper.ProjectTo<DestinationDerived>(new[] { new SourceDerived { BaseValue = 1, Other = "other", DerivedValue = 2 } }.AsQueryable()).Single();
        derived.BaseValue.ShouldBe(1);
        derived.Renamed.ShouldBe("other");
        derived.DerivedValue.ShouldBe(2);
        var closedBase = mapper.ProjectTo<DestinationBase>(new[] { new SourceBase<FooBar> { BaseValue = 3, Other = "base" } }.AsQueryable()).Single();
        closedBase.BaseValue.ShouldBe(3);
        closedBase.Renamed.ShouldBe("base");
    });
    static void InEveryOrder(Action<IProfileExpression>[] maps, Action<IMapper> check, Action<IMapperConfigurationExpression> global = null)
    {
        TypePair[] typeMaps = null;
        foreach (var order in Orders(maps.Length))
        {
            foreach (var inProfiles in new[] { false, true })
            {
                Should.NotThrow(() =>
                {
                    MapperConfiguration configuration = new(c =>
                    {
                        global?.Invoke(c);
                        foreach (var index in order)
                        {
                            if (inProfiles)
                            {
                                c.CreateProfile("Profile" + index, maps[index]);
                            }
                            else
                            {
                                maps[index](c);
                            }
                        }
                    });
                    configuration.AssertConfigurationIsValid();
                    var currentTypeMaps = configuration.GetAllTypeMaps().Select(typeMap => typeMap.Types).ToArray();
                    currentTypeMaps.ShouldBe(typeMaps ??= currentTypeMaps, ignoreOrder: true);
                    check(configuration.CreateMapper());
                }, $"maps registered in the order {string.Join(", ", order)}{(inProfiles ? ", each in a profile of its own" : null)}");
            }
        }
    }
    static IEnumerable<int[]> Orders(int count) => count == 1 ? [[0]] :
        from order in Orders(count - 1) from position in Enumerable.Range(0, count) select (int[])[.. order[..position], count - 1, .. order[position..]];
}
