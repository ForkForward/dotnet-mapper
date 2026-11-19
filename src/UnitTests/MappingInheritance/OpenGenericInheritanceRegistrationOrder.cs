namespace ForkForward.Mapper.UnitTests.MappingInheritance;
/// <summary>
/// A closed generic map that an include asks for is created from the open generic configuration and sealed while the profiles are still being
/// configured, and sealing it seals the maps it inherits from and the maps of its included members. Every fact builds its configuration once for
/// each order in which its maps can be registered, with the maps in the configuration itself and then with each map in a profile of its own.
/// The configuration has to be valid, hold the same type maps and map the same every time.
/// </summary>
public abstract class InEveryRegistrationOrder
{
    protected static void InEveryOrder(Action<IProfileExpression>[] maps, Action<IMapper> check, Action<IMapperConfigurationExpression> global = null)
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
/// <summary>
/// The open generic map that a derived map includes as its closed base has a base of its own. In 14.0.0 the configuration failed with a
/// NullReferenceException whenever the root map was registered after the derived map.
/// </summary>
public class OpenGenericBaseThatIncludesABaseInEveryRegistrationOrder : InEveryRegistrationOrder
{
    public class Entity
    {
        public string BaseMember { get; set; }
        public string Other { get; set; }
    }
    public class Entity<TId> : Entity
    {
        public TId Id { get; set; }
    }
    public class SubEntity : Entity<int>
    {
        public string SubMember { get; set; }
    }
    public class Model
    {
        public string BaseMember { get; set; }
        public string Renamed { get; set; }
        public int AfterMaps { get; set; }
    }
    public class Model<TId> : Model
    {
        public TId Id { get; set; }
    }
    public class SubModel : Model<int>
    {
        public string SubMember { get; set; }
    }
    static IMappingExpression<Entity, Model> Root(IProfileExpression c) => c.CreateMap<Entity, Model>()
        .ForMember(d => d.Renamed, o => o.MapFrom(s => s.Other))
        .ForMember(d => d.AfterMaps, o => o.Ignore())
        .AfterMap((_, destination) => destination.AfterMaps++);
    static void OpenBase(IProfileExpression c) => c.CreateMap(typeof(Entity<>), typeof(Model<>)).IncludeBase(typeof(Entity), typeof(Model));
    static void Derived(IProfileExpression c) => c.CreateMap<SubEntity, SubModel>().IncludeBase<Entity<int>, Model<int>>();
    static void ShouldMapEveryType(IMapper mapper) => ShouldMapEveryType(mapper, "base");
    static void ShouldMapEveryType(IMapper mapper, string baseMember)
    {
        var sub = mapper.Map<SubModel>(new SubEntity { BaseMember = "base", Other = "other", Id = 695, SubMember = "sub" });
        sub.BaseMember.ShouldBe(baseMember);
        sub.Renamed.ShouldBe("other");
        sub.Id.ShouldBe(695);
        sub.SubMember.ShouldBe("sub");
        sub.AfterMaps.ShouldBe(1);
        var closedBase = mapper.Map<Model<int>>(new Entity<int> { BaseMember = "base", Other = "other", Id = 42 });
        closedBase.ShouldBeOfType<Model<int>>();
        closedBase.BaseMember.ShouldBe(baseMember);
        closedBase.Renamed.ShouldBe("other");
        closedBase.Id.ShouldBe(42);
        closedBase.AfterMaps.ShouldBe(1);
        var root = mapper.Map<Model>(new Entity { BaseMember = "base", Other = "other" });
        root.ShouldBeOfType<Model>();
        root.BaseMember.ShouldBe(baseMember);
        root.Renamed.ShouldBe("other");
        root.AfterMaps.ShouldBe(1);
    }
    [Fact]
    public void The_open_generic_base_map_can_include_a_base_of_its_own() => InEveryOrder([c => Root(c), OpenBase, Derived], ShouldMapEveryType);
    [Fact]
    public void The_derived_map_can_include_the_root_map_as_well() => InEveryOrder([
        c => Root(c),
        OpenBase,
        c => c.CreateMap<SubEntity, SubModel>().IncludeBase<Entity<int>, Model<int>>().IncludeBase<Entity, Model>()], ShouldMapEveryType);
    [Fact]
    public void The_root_map_can_include_the_derived_map_as_well() => InEveryOrder([
        c => Root(c).Include<SubEntity, SubModel>(),
        OpenBase,
        Derived], ShouldMapEveryType);
    [Fact]
    public void Should_configure_every_map_once()
    {
        Dictionary<TypePair, int> configured = [];
        InEveryOrder([c => Root(c), OpenBase, Derived], mapper =>
        {
            configured.Keys.ShouldBe([new(typeof(Entity), typeof(Model)), new(typeof(Entity<>), typeof(Model<>)), new(typeof(SubEntity), typeof(SubModel)),
                new(typeof(Entity<int>), typeof(Model<int>))], ignoreOrder: true);
            configured.Values.ShouldAllBe(count => count == 1);
            ShouldMapEveryType(mapper);
        }, c =>
        {
            configured.Clear();
            c.ForAllMaps((typeMap, _) => configured[typeMap.Types] = configured.GetValueOrDefault(typeMap.Types) + 1);
        });
    }
    // A destination that the map creates itself (ConstructUsing), an abstract destination or a destination without a default constructor did not
    // fail in 14.0.0 when the root map was registered after the derived map: the root map was sealed before it was configured, and what ForAllMaps
    // did to it had no effect on the root map, on the closed base map or on the derived map. In the other orders it did.
    [Fact]
    public void ForAllMaps_should_configure_the_root_map_before_it_is_inherited() => InEveryOrder([
        c => Root(c).ConstructUsing(_ => new Model()),
        OpenBase,
        Derived], mapper => ShouldMapEveryType(mapper, baseMember: null), c => c.ForAllMaps((typeMap, map) =>
        {
            if (typeMap.Types == new TypePair(typeof(Entity), typeof(Model)))
            {
                map.ForMember(nameof(Model.BaseMember), o => o.Ignore());
            }
        }));
    [Fact]
    public void Two_closed_maps_can_be_created_from_the_open_generic_base_map() => InEveryOrder([
        c => Root(c),
        OpenBase,
        Derived,
        c => c.CreateMap<StringEntity, StringModel>().IncludeBase<Entity<string>, Model<string>>()], mapper =>
    {
        ShouldMapEveryType(mapper);
        var destination = mapper.Map<StringModel>(new StringEntity { BaseMember = "base", Other = "other", Id = "id" });
        destination.BaseMember.ShouldBe("base");
        destination.Renamed.ShouldBe("other");
        destination.Id.ShouldBe("id");
        destination.AfterMaps.ShouldBe(1);
    });
    public class StringEntity : Entity<string> { }
    public class StringModel : Model<string> { }
    [Fact]
    public void Should_map_back() => InEveryOrder([
        c => Root(c).ReverseMap(),
        c => c.CreateMap(typeof(Entity<>), typeof(Model<>)).IncludeBase(typeof(Entity), typeof(Model)).ReverseMap(),
        c => c.CreateMap<SubEntity, SubModel>().IncludeBase<Entity<int>, Model<int>>().ReverseMap()], mapper =>
    {
        ShouldMapEveryType(mapper);
        var sub = mapper.Map<SubEntity>(new SubModel { BaseMember = "base", Renamed = "other", Id = 695, SubMember = "sub" });
        sub.BaseMember.ShouldBe("base");
        sub.Other.ShouldBe("other");
        sub.Id.ShouldBe(695);
        sub.SubMember.ShouldBe("sub");
        var closedBase = mapper.Map<Entity<int>>(new Model<int> { BaseMember = "base", Renamed = "other", Id = 42 });
        closedBase.ShouldBeOfType<Entity<int>>();
        closedBase.BaseMember.ShouldBe("base");
        closedBase.Other.ShouldBe("other");
        closedBase.Id.ShouldBe(42);
    });
}
/// <summary>
/// A profile instance, and a configuration expression, can be used to build more than one configuration: each of them configures every map.
/// </summary>
public class ProfileUsedForTwoConfigurations
{
    public class Source
    {
        public int Value { get; set; }
        public string Other { get; set; }
    }
    public class DerivedSource : Source { }
    public class GenericSource<T> : Source { }
    public class ClosedSource : GenericSource<int> { }
    public class Destination
    {
        public int Value { get; set; }
        public string Renamed { get; set; }
        public string Ignored { get; set; }
    }
    public class DerivedDestination : Destination { }
    public class GenericDestination<T> : Destination { }
    public class ClosedDestination : GenericDestination<int> { }
    // Without it the configuration is not valid: nothing maps to Ignored.
    static void IgnoreMember(TypeMap typeMap, IMappingExpression map)
    {
        if (typeof(Destination).IsAssignableFrom(typeMap.DestinationType))
        {
            map.ForMember(nameof(Destination.Ignored), o => o.Ignore());
        }
    }
    class Maps : Profile
    {
        public int Configured;
        public Maps()
        {
            this.Internal().ForAllMaps((typeMap, map) =>
            {
                Configured++;
                IgnoreMember(typeMap, map);
            });
            CreateMap<ClosedSource, ClosedDestination>().IncludeBase<GenericSource<int>, GenericDestination<int>>();
            CreateMap<DerivedSource, DerivedDestination>().IncludeBase<Source, Destination>();
            CreateMap(typeof(GenericSource<>), typeof(GenericDestination<>)).IncludeBase(typeof(Source), typeof(Destination));
            CreateMap<Source, Destination>().ForMember(d => d.Renamed, o => o.MapFrom(s => s.Other)).ReverseMap();
        }
    }
    static void ShouldMap(MapperConfiguration configuration)
    {
        configuration.AssertConfigurationIsValid();
        var mapper = configuration.CreateMapper();
        mapper.Map<DerivedDestination>(new DerivedSource { Value = 1, Other = "other" }).Renamed.ShouldBe("other");
        mapper.Map<ClosedDestination>(new ClosedSource { Value = 1, Other = "other" }).Renamed.ShouldBe("other");
        mapper.Map<Source>(new Destination { Value = 1, Renamed = "other" }).Other.ShouldBe("other");
    }
    [Fact]
    public void Should_configure_every_map_of_a_profile_instance_each_time()
    {
        Maps profile = new();
        ShouldMap(new(c => c.AddProfile(profile)));
        // the four maps, the reverse map and the closed map the include creates
        profile.Configured.ShouldBe(6);
        ShouldMap(new(c => c.AddProfile(profile)));
        profile.Configured.ShouldBe(12);
    }
    [Fact]
    public void Should_configure_every_map_of_a_configuration_expression_each_time()
    {
        var configured = 0;
        MapperConfigurationExpression expression = new();
        expression.ForAllMaps((typeMap, map) =>
        {
            configured++;
            IgnoreMember(typeMap, map);
        });
        expression.CreateMap<DerivedSource, DerivedDestination>().IncludeBase<Source, Destination>();
        expression.CreateMap<Source, Destination>().ForMember(d => d.Renamed, o => o.MapFrom(s => s.Other));
        new MapperConfiguration(expression).AssertConfigurationIsValid();
        configured.ShouldBe(2);
        MapperConfiguration configuration = new(expression);
        configuration.AssertConfigurationIsValid();
        configured.ShouldBe(4);
        configuration.CreateMapper().Map<DerivedDestination>(new DerivedSource { Other = "other" }).Renamed.ShouldBe("other");
    }
}
/// <summary>
/// The root map that the open generic base map includes inherits from a map above it. When the root map names that map (IncludeBase), the order
/// does not matter. When that map names the root map (Include, IncludeAllDerived), it has to be registered before the map that includes the
/// closed base map, as in 14.0.0: registered later, it comes after the root map was sealed with the closed base map, and the root map does
/// not inherit from it.
/// </summary>
public class OpenGenericBaseWithTwoLevelsAboveItInEveryRegistrationOrder : InEveryRegistrationOrder
{
    public class Top
    {
        public string TopValue { get; set; }
    }
    public class Root : Top
    {
        public string RootValue { get; set; }
    }
    public class Generic<T> : Root
    {
        public T GenericValue { get; set; }
    }
    public class Leaf : Generic<int>
    {
        public string LeafValue { get; set; }
    }
    public class TopDto
    {
        public string TopValue { get; set; }
        public string FromTop { get; set; }
    }
    public class RootDto : TopDto
    {
        public string RootValue { get; set; }
    }
    public class GenericDto<T> : RootDto
    {
        public T GenericValue { get; set; }
    }
    public class LeafDto : GenericDto<int>
    {
        public string LeafValue { get; set; }
    }
    static IMappingExpression<Top, TopDto> TopMap(IProfileExpression c) => c.CreateMap<Top, TopDto>().ForMember(d => d.FromTop, o => o.MapFrom(_ => "top"));
    static void OpenBase(IProfileExpression c) => c.CreateMap(typeof(Generic<>), typeof(GenericDto<>)).IncludeBase(typeof(Root), typeof(RootDto));
    static void LeafMap(IProfileExpression c) => c.CreateMap<Leaf, LeafDto>().IncludeBase<Generic<int>, GenericDto<int>>();
    static void ShouldMapEveryType(IMapper mapper)
    {
        var leaf = mapper.Map<LeafDto>(new Leaf { TopValue = "0", RootValue = "1", GenericValue = 2, LeafValue = "3" });
        leaf.TopValue.ShouldBe("0");
        leaf.FromTop.ShouldBe("top");
        leaf.RootValue.ShouldBe("1");
        leaf.GenericValue.ShouldBe(2);
        leaf.LeafValue.ShouldBe("3");
        var closedBase = mapper.Map<GenericDto<int>>(new Generic<int> { TopValue = "0", RootValue = "1", GenericValue = 2 });
        closedBase.ShouldBeOfType<GenericDto<int>>();
        closedBase.TopValue.ShouldBe("0");
        closedBase.FromTop.ShouldBe("top");
        closedBase.RootValue.ShouldBe("1");
        closedBase.GenericValue.ShouldBe(2);
        var root = mapper.Map<RootDto>(new Root { TopValue = "0", RootValue = "1" });
        root.ShouldBeOfType<RootDto>();
        root.TopValue.ShouldBe("0");
        root.FromTop.ShouldBe("top");
        root.RootValue.ShouldBe("1");
        var top = mapper.Map<TopDto>(new Top { TopValue = "0" });
        top.ShouldBeOfType<TopDto>();
        top.TopValue.ShouldBe("0");
        top.FromTop.ShouldBe("top");
    }
    [Fact]
    public void The_root_map_can_include_its_base() => InEveryOrder([
        c => TopMap(c),
        c => c.CreateMap<Root, RootDto>().IncludeBase<Top, TopDto>(),
        OpenBase,
        LeafMap], ShouldMapEveryType);
    [Fact]
    public void The_root_map_can_be_included_by_a_base_that_is_registered_first() => InEveryOrder([
        c => c.CreateMap<Root, RootDto>(),
        OpenBase,
        LeafMap], ShouldMapEveryType, c => TopMap(c).Include<Root, RootDto>());
    [Fact]
    public void A_base_of_the_root_map_that_is_registered_first_can_include_all_derived_maps() => InEveryOrder([
        c => c.CreateMap<Root, RootDto>(),
        OpenBase,
        LeafMap], ShouldMapEveryType, c => TopMap(c).IncludeAllDerived());
}
/// <summary>
/// An open generic map includes another open generic map as its base, and a map includes the closed middle map. In 14.0.0 the configuration
/// failed with a NullReferenceException whenever the open generic base map was registered after the derived map.
/// </summary>
public class ChainedOpenGenericMapsInEveryRegistrationOrder : InEveryRegistrationOrder
{
    public class FooBar { }
    public interface ICountAfterMaps
    {
        int AfterMaps { get; set; }
    }
    public class SourceBase<T> where T : class
    {
        public int BaseValue { get; set; }
        public string Other { get; set; }
    }
    public class SourceMiddle<T> : SourceBase<T> where T : class
    {
        public int MiddleValue { get; set; }
        public string MiddleOther { get; set; }
    }
    public class SourceDerived : SourceMiddle<FooBar>
    {
        public int DerivedValue { get; set; }
    }
    public class DestinationBase : ICountAfterMaps
    {
        public int BaseValue { get; set; }
        public string Renamed { get; set; }
        public int AfterMaps { get; set; }
    }
    public class DestinationMiddle : DestinationBase
    {
        public int MiddleValue { get; set; }
        public string MiddleRenamed { get; set; }
    }
    public class DestinationDerived : DestinationMiddle
    {
        public int DerivedValue { get; set; }
    }
    static void OpenBase(IProfileExpression c) => c.CreateMap(typeof(SourceBase<>), typeof(DestinationBase))
        .ForMember(nameof(DestinationBase.Renamed), o => o.MapFrom(nameof(SourceBase<FooBar>.Other)))
        .ForMember(nameof(DestinationBase.AfterMaps), o => o.Ignore())
        .AfterMap((_, destination) => ((ICountAfterMaps)destination).AfterMaps++);
    static void OpenMiddle(IProfileExpression c) => c.CreateMap(typeof(SourceMiddle<>), typeof(DestinationMiddle))
        .ForMember(nameof(DestinationMiddle.MiddleRenamed), o => o.MapFrom(nameof(SourceMiddle<FooBar>.MiddleOther)))
        .IncludeBase(typeof(SourceBase<>), typeof(DestinationBase))
        .AfterMap((_, destination) => ((ICountAfterMaps)destination).AfterMaps += 10);
    static void Derived(IProfileExpression c) => c.CreateMap<SourceDerived, DestinationDerived>()
        .IncludeBase<SourceMiddle<FooBar>, DestinationMiddle>()
        .AfterMap((_, destination) => destination.AfterMaps += 100);
    [Fact]
    public void Open_generic_maps_can_be_chained() => InEveryOrder([OpenBase, OpenMiddle, Derived], mapper =>
    {
        var derived = mapper.Map<DestinationDerived>(new SourceDerived { BaseValue = 1, Other = "other", MiddleValue = 2, MiddleOther = "middle", DerivedValue = 3 });
        derived.BaseValue.ShouldBe(1);
        derived.Renamed.ShouldBe("other");
        derived.MiddleValue.ShouldBe(2);
        derived.MiddleRenamed.ShouldBe("middle");
        derived.DerivedValue.ShouldBe(3);
        derived.AfterMaps.ShouldBe(111);
        var middle = mapper.Map<DestinationMiddle>(new SourceMiddle<FooBar> { BaseValue = 1, Other = "other", MiddleValue = 2, MiddleOther = "middle" });
        middle.ShouldBeOfType<DestinationMiddle>();
        middle.BaseValue.ShouldBe(1);
        middle.Renamed.ShouldBe("other");
        middle.MiddleValue.ShouldBe(2);
        middle.MiddleRenamed.ShouldBe("middle");
        middle.AfterMaps.ShouldBe(11);
        var closedBase = mapper.Map<DestinationBase>(new SourceBase<FooBar> { BaseValue = 1, Other = "other" });
        closedBase.ShouldBeOfType<DestinationBase>();
        closedBase.BaseValue.ShouldBe(1);
        closedBase.Renamed.ShouldBe("other");
        closedBase.AfterMaps.ShouldBe(1);
        var projected = mapper.ProjectTo<DestinationDerived>(new[] { new SourceDerived { BaseValue = 1, Other = "other", MiddleValue = 2, MiddleOther = "middle", DerivedValue = 3 } }.AsQueryable()).Single();
        projected.BaseValue.ShouldBe(1);
        projected.Renamed.ShouldBe("other");
        projected.MiddleValue.ShouldBe(2);
        projected.MiddleRenamed.ShouldBe("middle");
        projected.DerivedValue.ShouldBe(3);
    });
    public class Entity<T>
    {
        public T Id { get; set; }
        public string Name { get; set; }
    }
    public class Audited : Entity<int>
    {
        public string Author { get; set; }
    }
    public class Versioned<T> : Audited
    {
        public T Version { get; set; }
    }
    public class Document : Versioned<Guid>
    {
        public string Title { get; set; }
    }
    public class EntityDto<T>
    {
        public T Id { get; set; }
        public string DisplayName { get; set; }
    }
    public class AuditedDto : EntityDto<int>
    {
        public string AuthorName { get; set; }
    }
    public class VersionedDto<T> : AuditedDto
    {
        public T Version { get; set; }
    }
    public class DocumentDto : VersionedDto<Guid>
    {
        public string Title { get; set; }
    }
    [Fact]
    public void Closed_and_open_generic_bases_can_alternate() => InEveryOrder([
        c => c.CreateMap(typeof(Entity<>), typeof(EntityDto<>)).ForMember("DisplayName", o => o.MapFrom("Name")),
        c => c.CreateMap<Audited, AuditedDto>().IncludeBase<Entity<int>, EntityDto<int>>().ForMember(d => d.AuthorName, o => o.MapFrom(s => s.Author)),
        c => c.CreateMap(typeof(Versioned<>), typeof(VersionedDto<>)).IncludeBase(typeof(Audited), typeof(AuditedDto)),
        c => c.CreateMap<Document, DocumentDto>().IncludeBase<Versioned<Guid>, VersionedDto<Guid>>()], mapper =>
    {
        var version = Guid.NewGuid();
        var document = mapper.Map<DocumentDto>(new Document { Id = 1, Name = "name", Author = "author", Version = version, Title = "title" });
        document.Id.ShouldBe(1);
        document.DisplayName.ShouldBe("name");
        document.AuthorName.ShouldBe("author");
        document.Version.ShouldBe(version);
        document.Title.ShouldBe("title");
        var versioned = mapper.Map<VersionedDto<Guid>>(new Versioned<Guid> { Id = 1, Name = "name", Author = "author", Version = version });
        versioned.ShouldBeOfType<VersionedDto<Guid>>();
        versioned.Id.ShouldBe(1);
        versioned.DisplayName.ShouldBe("name");
        versioned.AuthorName.ShouldBe("author");
        versioned.Version.ShouldBe(version);
        var audited = mapper.Map<AuditedDto>(new Audited { Id = 1, Name = "name", Author = "author" });
        audited.ShouldBeOfType<AuditedDto>();
        audited.Id.ShouldBe(1);
        audited.DisplayName.ShouldBe("name");
        audited.AuthorName.ShouldBe("author");
        var entity = mapper.Map<EntityDto<int>>(new Entity<int> { Id = 1, Name = "name" });
        entity.ShouldBeOfType<EntityDto<int>>();
        entity.Id.ShouldBe(1);
        entity.DisplayName.ShouldBe("name");
    });
}
/// <summary>
/// The map of an included member is a closed generic map: IncludeMembers creates it from the open generic configuration, and sealing it seals
/// the maps of its own included members, and the map it inherits them from. In 14.0.0 the configuration failed with a NullReferenceException
/// whenever one of those was registered after the map that includes the member.
/// </summary>
public class IncludeMembersOfAClosedGenericMapInEveryRegistrationOrder : InEveryRegistrationOrder
{
    public class Source
    {
        public string Name { get; set; }
        public Wrapper<Inner> Wrapper { get; set; }
    }
    public class Wrapper<T>
    {
        public string Description { get; set; }
        public T Value { get; set; }
    }
    public class Inner
    {
        public string Title { get; set; }
        public string Code { get; set; }
    }
    public class Destination
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Title { get; set; }
        public string Code { get; set; }
    }
    public class DerivedSource
    {
        public string Name { get; set; }
        public DerivedWrapper<int> Wrapper { get; set; }
    }
    public class WrapperBase
    {
        public string Description { get; set; }
        public Inner Value { get; set; }
    }
    public class DerivedWrapper<T> : WrapperBase
    {
        public T Extra { get; set; }
    }
    public class Other
    {
        public string Code { get; set; }
    }
    public class SourceWithOther
    {
        public string Name { get; set; }
        public string Title { get; set; }
        public DerivedWrapper<int> Wrapper { get; set; }
        public Other Other { get; set; }
    }
    static void InnerMap(IProfileExpression c) => c.CreateMap<Inner, Destination>(MemberList.None);
    static void WrapperBaseMap(IProfileExpression c) => c.CreateMap<WrapperBase, Destination>(MemberList.None).IncludeMembers(s => s.Value);
    static void ShouldMap(IMapper mapper, object source, object wrapper)
    {
        var destination = mapper.Map<Destination>(source);
        destination.Name.ShouldBe("name");
        destination.Description.ShouldBe("description");
        destination.Title.ShouldBe("title");
        destination.Code.ShouldBe("code");
        var fromWrapper = mapper.Map<Destination>(wrapper);
        fromWrapper.Name.ShouldBeNull();
        fromWrapper.Description.ShouldBe("description");
        fromWrapper.Title.ShouldBe("title");
        fromWrapper.Code.ShouldBe("code");
        var fromInner = mapper.Map<Destination>(new Inner { Title = "title", Code = "code" });
        fromInner.Description.ShouldBeNull();
        fromInner.Title.ShouldBe("title");
        fromInner.Code.ShouldBe("code");
    }
    [Fact]
    public void The_closed_generic_map_can_include_members_itself() => InEveryOrder([
        c => c.CreateMap<Source, Destination>().IncludeMembers(s => s.Wrapper),
        c => c.CreateMap(typeof(Wrapper<>), typeof(Destination), MemberList.None).IncludeMembers("Value"),
        InnerMap], mapper =>
    {
        Wrapper<Inner> wrapper = new() { Description = "description", Value = new() { Title = "title", Code = "code" } };
        ShouldMap(mapper, new Source { Name = "name", Wrapper = wrapper }, wrapper);
        var projected = mapper.ProjectTo<Destination>(new[] { new Source { Name = "name", Wrapper = wrapper } }.AsQueryable()).Single();
        projected.Name.ShouldBe("name");
        projected.Description.ShouldBe("description");
        projected.Title.ShouldBe("title");
        projected.Code.ShouldBe("code");
        mapper.Map<Destination>(new Source { Name = "name" }).Title.ShouldBeNull();
    });
    [Fact]
    public void The_closed_generic_map_can_inherit_the_included_members() => InEveryOrder([
        c => c.CreateMap<DerivedSource, Destination>().IncludeMembers(s => s.Wrapper),
        c => c.CreateMap(typeof(DerivedWrapper<>), typeof(Destination), MemberList.None).IncludeBase(typeof(WrapperBase), typeof(Destination)),
        WrapperBaseMap,
        InnerMap], mapper =>
    {
        DerivedWrapper<int> wrapper = new() { Description = "description", Value = new() { Title = "title", Code = "code" } };
        ShouldMap(mapper, new DerivedSource { Name = "name", Wrapper = wrapper }, wrapper);
        var projected = mapper.ProjectTo<Destination>(new[] { new DerivedSource { Name = "name", Wrapper = wrapper } }.AsQueryable()).Single();
        projected.Name.ShouldBe("name");
        projected.Description.ShouldBe("description");
        projected.Title.ShouldBe("title");
        projected.Code.ShouldBe("code");
        mapper.Map<Destination>(new DerivedSource { Name = "name" }).Title.ShouldBeNull();
        mapper.Map<Destination>(new DerivedSource { Name = "name", Wrapper = new() }).Title.ShouldBeNull();
    });
    // What the orders that worked in 14.0.0 give, kept in every order: the closed generic map ignores Code, and the map that includes it takes
    // Code from the member the closed generic map inherits, ahead of a member it includes later.
    [Fact]
    public void A_member_the_closed_generic_map_ignores_should_come_from_its_inherited_included_member() => InEveryOrder([
        c => c.CreateMap<SourceWithOther, Destination>().IncludeMembers(s => s.Wrapper, s => s.Other),
        c => c.CreateMap(typeof(DerivedWrapper<>), typeof(Destination), MemberList.None).IncludeBase(typeof(WrapperBase), typeof(Destination))
            .ForMember(nameof(Destination.Code), o => o.Ignore()),
        WrapperBaseMap,
        InnerMap,
        c => c.CreateMap<Other, Destination>(MemberList.None)], mapper =>
    {
        var destination = mapper.Map<Destination>(new SourceWithOther
        {
            Name = "name", Title = "own title", Other = new() { Code = "other code" },
            Wrapper = new() { Description = "description", Value = new() { Title = "title", Code = "code" } }
        });
        destination.Name.ShouldBe("name");
        destination.Description.ShouldBe("description");
        destination.Title.ShouldBe("own title");
        destination.Code.ShouldBe("code");
    });
}
/// <summary>
/// A configuration that works in every order in 14.0.0, and that sealing the closed base map later would break: the derived map has a member
/// that is mapped by the map of a base class of its type, and the closed base map knows the derived map by the time it would be sealed.
/// </summary>
public class MemberOfTheDerivedMapMappedByABaseClassMapInEveryRegistrationOrder : InEveryRegistrationOrder
{
    public class FooBar { }
    public class ChildBase
    {
        public int Value { get; set; }
    }
    public class ChildDerived : ChildBase { }
    public class ChildDto
    {
        public int Value { get; set; }
    }
    public class SourceBase<T>
    {
        public int BaseValue { get; set; }
    }
    public class SourceDerived : SourceBase<FooBar>
    {
        public ChildDerived Child { get; set; }
    }
    public class DestinationBase
    {
        public int BaseValue { get; set; }
    }
    public class DestinationDerived : DestinationBase
    {
        public ChildDto Child { get; set; }
    }
    [Fact]
    public void Should_map_the_member_with_the_map_of_its_base_class() => InEveryOrder([
        c => c.CreateMap(typeof(SourceBase<>), typeof(DestinationBase)),
        c => c.CreateMap<SourceDerived, DestinationDerived>().IncludeBase<SourceBase<FooBar>, DestinationBase>(),
        c => c.CreateMap<ChildBase, ChildDto>()], mapper =>
    {
        var derived = mapper.Map<DestinationDerived>(new SourceDerived { BaseValue = 1, Child = new() { Value = 2 } });
        derived.BaseValue.ShouldBe(1);
        derived.Child.Value.ShouldBe(2);
        var closedBase = mapper.Map<DestinationBase>(new SourceBase<FooBar> { BaseValue = 3 });
        closedBase.ShouldBeOfType<DestinationBase>();
        closedBase.BaseValue.ShouldBe(3);
    });
}
