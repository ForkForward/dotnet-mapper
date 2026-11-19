namespace ForkForward.Mapper.UnitTests;

// A member that is not inlined into the plan of its map (MapAtRuntime, or inlining used up) is mapped by a plan of
// its own. For a collection member of a map with MaxDepth that plan asks whether the map is over its depth, and depth
// is counted in a context of its own, which only a map with MaxDepth or PreserveReferences creates. The plan used to
// be cached by the source and destination types alone, so a member of a map with neither, with the same two types,
// ran it with the mapper's default context, and the question ended in "Context.Items are only available when using a
// Map overload that takes Action<IMappingOperationOptions>!", from a map that never touches Context.Items, and only
// when the map with MaxDepth had been used first. Plans are cached per member now (see MapRequest and
// PlansOfMembersMappedThroughTheContext); the default context still answers "no", as a safeguard. These tests pin
// that both maps map, in either order.
public class MaxDepthWithMembersMappedAtRuntime
{
    public enum Limit { None, MaxDepth, PreserveReferences }
    public enum NotInlinedBecause { MapAtRuntime, InliningUsedUp }

    class Service
    {
        public string Description { get; set; }
    }
    class ServiceDto
    {
        public string Description { get; set; }
    }
    class RecursiveLine
    {
        public RecursiveLine Parent { get; set; }
        public List<Service> Services { get; set; }
    }
    class RecursiveLineDto
    {
        public RecursiveLineDto Parent { get; set; }
        public IList<ServiceDto> Services { get; set; }
    }
    class PlainLine
    {
        public List<Service> Services { get; set; }
    }
    class PlainLineDto
    {
        public IList<ServiceDto> Services { get; set; }
    }

    public static TheoryData<Limit, NotInlinedBecause, bool> Cases()
    {
        TheoryData<Limit, NotInlinedBecause, bool> cases = [];
        foreach (var limit in Enum.GetValues<Limit>())
        {
            foreach (var notInlinedBecause in Enum.GetValues<NotInlinedBecause>())
            {
                cases.Add(limit, notInlinedBecause, true);
                cases.Add(limit, notInlinedBecause, false);
            }
        }
        return cases;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Two_maps_with_a_member_of_the_same_types_both_map_whichever_is_used_first(Limit limit, NotInlinedBecause notInlinedBecause, bool recursiveFirst)
    {
        var mapper = Configuration(limit, notInlinedBecause).CreateMapper();

        // Before: with MaxDepth, and the recursive map used first, the plain map threw an AutoMapperMappingException
        // around the InvalidOperationException about Context.Items. Every other case mapped, as it does now.
        if (recursiveFirst)
        {
            MapRecursive(mapper);
            MapPlain(mapper);
        }
        else
        {
            MapPlain(mapper);
            MapRecursive(mapper);
        }
        MapRecursive(mapper);
        MapPlain(mapper);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Mappers_created_from_one_configuration_map_both_too(bool recursiveFirst)
    {
        var configuration = Configuration(Limit.MaxDepth, NotInlinedBecause.MapAtRuntime);
        var first = configuration.CreateMapper();
        var second = configuration.CreateMapper();

        // Before: the plain map threw on the second mapper once the first one had used the recursive map.
        if (recursiveFirst)
        {
            MapRecursive(first);
            MapPlain(second);
        }
        else
        {
            MapPlain(first);
            MapRecursive(second);
        }
        MapPlain(first);
        MapRecursive(second);
    }

    [Fact]
    public void A_map_with_options_maps_the_member_with_its_own_context()
    {
        var mapper = Configuration(Limit.MaxDepth, NotInlinedBecause.MapAtRuntime).CreateMapper();
        MapRecursive(mapper);

        // Unchanged: a Map call with options has a context of its own, in which the depth can be asked for.
        var destination = mapper.Map<PlainLineDto>(new PlainLine { Services = [new Service { Description = "plain" }] }, options => options.Items["key"] = "value");

        destination.Services.Single().Description.ShouldBe("plain");
    }

    [Fact]
    public void The_map_without_MaxDepth_keeps_the_default_context_of_its_mapper()
    {
        List<ResolutionContext> contexts = [];
        var mapper = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Service, ServiceDto>().AfterMap((_, _, context) => contexts.Add(context));
            cfg.CreateMap<RecursiveLine, RecursiveLineDto>().MaxDepth(5).ForMember(d => d.Services, o => o.MapAtRuntime());
            cfg.CreateMap<PlainLine, PlainLineDto>().ForMember(d => d.Services, o => o.MapAtRuntime());
        }).CreateMapper();
        MapRecursive(mapper);
        contexts.Clear();

        MapPlain(mapper);
        MapPlain(mapper);

        // The fix must not cost the map without MaxDepth a context per Map call: both calls ran with the same one,
        // the mapper's default context, which holds no state.
        contexts.Count.ShouldBe(2);
        contexts[1].ShouldBeSameAs(contexts[0]);
        Should.Throw<InvalidOperationException>(() => contexts[0].InstanceCache);
    }

    static MapperConfiguration Configuration(Limit limit, NotInlinedBecause notInlinedBecause) => new(cfg =>
    {
        if (notInlinedBecause == NotInlinedBecause.InliningUsedUp)
        {
            cfg.Internal().MaxExecutionPlanDepth = 0;
        }
        cfg.CreateMap<Service, ServiceDto>();
        var recursive = cfg.CreateMap<RecursiveLine, RecursiveLineDto>();
        var plain = cfg.CreateMap<PlainLine, PlainLineDto>();
        switch (limit)
        {
            case Limit.MaxDepth:
                recursive.MaxDepth(5);
                break;
            case Limit.PreserveReferences:
                recursive.PreserveReferences();
                break;
        }
        if (notInlinedBecause == NotInlinedBecause.MapAtRuntime)
        {
            recursive.ForMember(d => d.Services, o => o.MapAtRuntime());
            plain.ForMember(d => d.Services, o => o.MapAtRuntime());
        }
    });

    static void MapRecursive(IMapper mapper)
    {
        var source = new RecursiveLine { Services = [new Service { Description = "recursive" }], Parent = new RecursiveLine { Services = [new Service { Description = "parent" }] } };
        var destination = mapper.Map<RecursiveLineDto>(source);
        destination.Services.Single().Description.ShouldBe("recursive");
        destination.Parent.Services.Single().Description.ShouldBe("parent");
    }

    static void MapPlain(IMapper mapper) =>
        mapper.Map<PlainLineDto>(new PlainLine { Services = [new Service { Description = "plain" }] }).Services.Single().Description.ShouldBe("plain");
}
