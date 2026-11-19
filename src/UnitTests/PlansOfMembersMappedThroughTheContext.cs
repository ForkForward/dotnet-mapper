using ForkForward.Mapper.Internal.Mappers;

namespace ForkForward.Mapper.UnitTests;

// A member that is not inlined into the plan of its map (MapAtRuntime, inlining used up, a member whose type map
// preserves references, a source typed object) is mapped through the context, by a plan of its own that is built
// when it is first needed. The built-in mappers build that plan for the member: the plan of a collection member of
// a map with MaxDepth asks whether that map is over its depth, the plan of a member that must use the destination
// value fills the collection it is given, and a mapping error names the member. The plan was cached by the source
// and destination types alone, so every member with the same two types ran the plan built for the first of them:
// what a member did depended on which map of the application had been used before. The cache now keeps the plan a
// built-in mapper builds per member; what is still shared is pinned at the end.
public class PlansOfMembersMappedThroughTheContext
{
    public enum NotInlinedBecause { MapAtRuntime, InliningUsedUp, PreserveReferences }

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
        public PlainLine Attachment { get; set; }
    }
    class RecursiveLineDto
    {
        public RecursiveLineDto Parent { get; set; }
        public IList<ServiceDto> Services { get; set; }
        public PlainLineDto Attachment { get; set; }
    }
    class PlainLine
    {
        public List<Service> Services { get; set; }
    }
    class PlainLineDto
    {
        public IList<ServiceDto> Services { get; set; }
    }

    public static TheoryData<NotInlinedBecause, bool> Cases()
    {
        TheoryData<NotInlinedBecause, bool> cases = [];
        foreach (var notInlinedBecause in Enum.GetValues<NotInlinedBecause>())
        {
            cases.Add(notInlinedBecause, true);
            cases.Add(notInlinedBecause, false);
        }
        return cases;
    }

    // MaxDepth: the collection member of the map with MaxDepth is left empty on the last level, the one of the map
    // without MaxDepth never is.

    [Theory]
    [MemberData(nameof(Cases))]
    public void A_collection_member_stops_at_the_MaxDepth_of_its_own_map_whichever_map_is_used_first(NotInlinedBecause notInlinedBecause, bool recursiveFirst)
    {
        var mapper = MaxDepthConfiguration(notInlinedBecause).CreateMapper();

        // Before: 1, 1, 1, 1, 0 when the recursive map was used first, and 1, 1, 1, 1, 1 when the plain map was: the
        // plan built for the member of the plain map does not ask for the depth.
        if (recursiveFirst)
        {
            ServicesPerLevel(mapper.Map<RecursiveLineDto>(Chain(8))).ShouldBe([1, 1, 1, 1, 0]);
            mapper.Map<PlainLineDto>(Plain()).Services.Count.ShouldBe(1);
        }
        else
        {
            mapper.Map<PlainLineDto>(Plain()).Services.Count.ShouldBe(1);
            ServicesPerLevel(mapper.Map<RecursiveLineDto>(Chain(8))).ShouldBe([1, 1, 1, 1, 0]);
        }
        ServicesPerLevel(mapper.Map<RecursiveLineDto>(Chain(8))).ShouldBe([1, 1, 1, 1, 0]);
        mapper.Map<PlainLineDto>(Plain()).Services.Count.ShouldBe(1);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void One_Map_call_that_maps_both_maps_stops_only_the_member_of_the_map_with_MaxDepth(NotInlinedBecause notInlinedBecause, bool recursiveFirst)
    {
        var mapper = MaxDepthConfiguration(notInlinedBecause).CreateMapper();
        if (recursiveFirst)
        {
            mapper.Map<RecursiveLineDto>(Chain(1));
        }
        else
        {
            mapper.Map<PlainLineDto>(Plain());
        }

        var destination = mapper.Map<RecursiveLineDto>(Chain(8, withAttachments: true));

        // Before: with the plan built for the recursive map, the plain line attached to the last level lost its
        // services, because the recursive map was over its depth; with the plan built for the plain map, the last
        // level of the recursive map kept its own.
        ServicesPerLevel(destination).ShouldBe([1, 1, 1, 1, 0]);
        Levels(destination).Select(level => level.Attachment.Services.Count).ShouldBe([1, 1, 1, 1, 1]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Mappers_created_from_one_configuration_give_each_member_its_own_plan_too(bool recursiveFirst)
    {
        var configuration = MaxDepthConfiguration(NotInlinedBecause.MapAtRuntime);
        var first = configuration.CreateMapper();
        var second = configuration.CreateMapper();

        if (recursiveFirst)
        {
            ServicesPerLevel(first.Map<RecursiveLineDto>(Chain(8))).ShouldBe([1, 1, 1, 1, 0]);
            second.Map<PlainLineDto>(Plain()).Services.Count.ShouldBe(1);
        }
        else
        {
            first.Map<PlainLineDto>(Plain()).Services.Count.ShouldBe(1);
            ServicesPerLevel(second.Map<RecursiveLineDto>(Chain(8))).ShouldBe([1, 1, 1, 1, 0]);
        }
        ServicesPerLevel(second.Map<RecursiveLineDto>(Chain(8, withAttachments: true))).ShouldBe([1, 1, 1, 1, 0]);
        Levels(first.Map<RecursiveLineDto>(Chain(8, withAttachments: true))).Select(level => level.Attachment.Services.Count).ShouldBe([1, 1, 1, 1, 1]);
    }

    static MapperConfiguration MaxDepthConfiguration(NotInlinedBecause notInlinedBecause) => new(cfg =>
    {
        if (notInlinedBecause == NotInlinedBecause.InliningUsedUp)
        {
            cfg.Internal().MaxExecutionPlanDepth = 0;
        }
        var service = cfg.CreateMap<Service, ServiceDto>();
        var recursive = cfg.CreateMap<RecursiveLine, RecursiveLineDto>().MaxDepth(5);
        var plain = cfg.CreateMap<PlainLine, PlainLineDto>();
        switch (notInlinedBecause)
        {
            case NotInlinedBecause.MapAtRuntime:
                recursive.ForMember(d => d.Services, o => o.MapAtRuntime());
                plain.ForMember(d => d.Services, o => o.MapAtRuntime());
                break;
            case NotInlinedBecause.PreserveReferences:
                service.PreserveReferences();
                break;
        }
    });

    static RecursiveLine Chain(int levels, bool withAttachments = false)
    {
        RecursiveLine line = null;
        for (var level = levels; level > 0; level--)
        {
            line = new() { Parent = line, Services = [new() { Description = "level " + level }], Attachment = withAttachments ? Plain() : null };
        }
        return line;
    }

    static PlainLine Plain() => new() { Services = [new() { Description = "plain" }] };

    static IEnumerable<RecursiveLineDto> Levels(RecursiveLineDto line)
    {
        for (; line != null; line = line.Parent)
        {
            yield return line;
        }
    }

    static int[] ServicesPerLevel(RecursiveLineDto line) => [.. Levels(line).Select(level => level.Services.Count)];

    // Errors: the member named in the error is the one that was being mapped.

    class Unmapped { }
    class UnmappedDto { }
    class Parcel
    {
        public Unmapped Content { get; set; }
    }
    class ParcelDto
    {
        public UnmappedDto Content { get; set; }
    }
    class Letter
    {
        public Unmapped Enclosure { get; set; }
    }
    class LetterDto
    {
        public UnmappedDto Enclosure { get; set; }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_missing_map_names_the_member_it_is_missing_for_whichever_map_is_used_first(bool parcelFirst)
    {
        var mapper = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Parcel, ParcelDto>();
            cfg.CreateMap<Letter, LetterDto>();
        }).CreateMapper();
        TypePair unmapped = new(typeof(Unmapped), typeof(UnmappedDto));
        void MapParcel() => ShouldName<Parcel, ParcelDto>(MemberError(() => mapper.Map<ParcelDto>(new Parcel { Content = new() }), unmapped), nameof(ParcelDto.Content));
        void MapLetter() => ShouldName<Letter, LetterDto>(MemberError(() => mapper.Map<LetterDto>(new Letter { Enclosure = new() }), unmapped), nameof(LetterDto.Enclosure));

        // Before: both errors named the member of the map that was used first.
        if (parcelFirst)
        {
            MapParcel();
            MapLetter();
        }
        else
        {
            MapLetter();
            MapParcel();
        }
        MapParcel();
        MapLetter();
    }

    class Reading
    {
        public object Value { get; set; }
    }
    class ReadingDto
    {
        public int Value { get; set; }
    }
    class Sample
    {
        public object Amount { get; set; }
    }
    class SampleDto
    {
        public int Amount { get; set; }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_mapping_error_names_the_member_that_was_being_mapped_whichever_map_is_used_first(bool readingFirst)
    {
        var mapper = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Reading, ReadingDto>();
            cfg.CreateMap<Sample, SampleDto>();
        }).CreateMapper();
        TypePair stringToInt = new(typeof(string), typeof(int));
        void MapReading()
        {
            mapper.Map<ReadingDto>(new Reading { Value = "42" }).Value.ShouldBe(42);
            var error = MemberError(() => mapper.Map<ReadingDto>(new Reading { Value = "not a number" }), stringToInt);
            ShouldName<Reading, ReadingDto>(error, nameof(ReadingDto.Value));
            error.InnerException.ShouldBeOfType<FormatException>();
        }
        void MapSample()
        {
            mapper.Map<SampleDto>(new Sample { Amount = "42" }).Amount.ShouldBe(42);
            var error = MemberError(() => mapper.Map<SampleDto>(new Sample { Amount = "not a number" }), stringToInt);
            ShouldName<Sample, SampleDto>(error, nameof(SampleDto.Amount));
            error.InnerException.ShouldBeOfType<FormatException>();
        }

        // Before: both errors named the member of the map that was used first.
        if (readingFirst)
        {
            MapReading();
            MapSample();
        }
        else
        {
            MapSample();
            MapReading();
        }
        MapReading();
        MapSample();
    }

    class Uncompilable { }
    class UncompilableDto { }
    class Crate
    {
        public Uncompilable Content { get; set; }
    }
    class CrateDto
    {
        public UncompilableDto Content { get; set; }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_type_map_whose_plan_cannot_be_compiled_names_the_member_it_was_mapped_for_and_only_then(bool memberFirst)
    {
        // The plan of Uncompilable -> UncompilableDto can be built, but not compiled: nothing declares the variable.
        var undeclared = Expression.Lambda<Func<Uncompilable, UncompilableDto>>(
            Expression.Variable(typeof(UncompilableDto), "undeclared"), Expression.Parameter(typeof(Uncompilable), "source"));
        var mapper = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Uncompilable, UncompilableDto>().ConvertUsing(undeclared);
            cfg.CreateMap<Crate, CrateDto>().ForMember(d => d.Content, o => o.MapAtRuntime());
        }).CreateMapper();
        TypePair uncompilable = new(typeof(Uncompilable), typeof(UncompilableDto));
        void MapMember()
        {
            var error = MemberError(() => mapper.Map<CrateDto>(new Crate { Content = new() }), uncompilable);
            error.Message.ShouldStartWith("Error building the mapping plan.", Case.Sensitive);
            ShouldName<Crate, CrateDto>(error, nameof(CrateDto.Content));
        }
        void MapWithoutMember()
        {
            var error = MemberError(() => mapper.Map<Uncompilable, UncompilableDto>(new Uncompilable()), uncompilable);
            error.Message.ShouldStartWith("Error building the mapping plan.", Case.Sensitive);
            error.MemberMap.ShouldBeNull();
        }

        // The plan of a type map is compiled once for all the members that map through it. When that fails, the error
        // still says which member it was needed for. Before: both errors were the one of the call that came first.
        if (memberFirst)
        {
            MapMember();
            MapWithoutMember();
        }
        else
        {
            MapWithoutMember();
            MapMember();
        }
        MapMember();
        MapWithoutMember();
    }

    // The error for the given pair of types: the one the plan of the member throws. A compiled plan of the map wraps
    // it once more, as an error while mapping the member; an interpreted plan does not (see InterpretedPlans.cs).
    static AutoMapperMappingException MemberError(Action map, TypePair types)
    {
        for (Exception exception = Should.Throw<AutoMapperMappingException>(map); exception != null; exception = exception.InnerException)
        {
            if (exception is AutoMapperMappingException mappingException && mappingException.Types == types)
            {
                return mappingException;
            }
        }
        throw new InvalidOperationException($"No error for {types.SourceType} -> {types.DestinationType}.");
    }

    static void ShouldName<TSource, TDestination>(AutoMapperMappingException exception, string member)
    {
        exception.MemberMap.ShouldNotBeNull();
        exception.MemberMap.DestinationName.ShouldBe(member);
        exception.MemberMap.TypeMap.Types.ShouldBe(new TypePair(typeof(TSource), typeof(TDestination)));
        exception.Message.ShouldContain("Destination Member:" + Environment.NewLine + member + Environment.NewLine, Case.Sensitive);
    }

    // The destination value: a member that must use it fills the collection it is given, and fails without one;
    // another member gets a new collection when it is given none.

    class Basket
    {
        public List<Service> Services { get; set; }
    }
    class BasketDto
    {
        public List<ServiceDto> Services { get; set; }
    }
    class KeptBasketDto
    {
        public List<ServiceDto> Services { get; set; } = [];
    }
    class ReadOnlyBasketDto
    {
        public List<ServiceDto> Services { get; } = [];
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void UseDestinationValue_is_the_setting_of_the_member_being_mapped_whichever_map_is_used_first(bool useDestinationValueFirst)
    {
        var mapper = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Service, ServiceDto>();
            cfg.CreateMap<Basket, KeptBasketDto>().ForMember(d => d.Services, o =>
            {
                o.UseDestinationValue();
                o.MapAtRuntime();
            });
            cfg.CreateMap<Basket, BasketDto>().ForMember(d => d.Services, o => o.MapAtRuntime());
        }).CreateMapper();
        void Kept()
        {
            KeptBasketDto destination = new();
            var collection = destination.Services;
            mapper.Map(TwoServices(), destination);
            destination.Services.ShouldBeSameAs(collection);
            collection.Select(service => service.Description).ShouldBe(["one", "two"]);
        }
        void New() => mapper.Map<BasketDto>(TwoServices()).Services.Select(service => service.Description).ShouldBe(["one", "two"]);

        // Before: with the plan built for the member that uses the destination value, the other member, which has no
        // destination value in a new object, failed with a NullReferenceException inside an AutoMapperMappingException.
        if (useDestinationValueFirst)
        {
            Kept();
            New();
        }
        else
        {
            New();
            Kept();
        }
        Kept();
        New();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_member_without_a_setter_and_one_with_a_setter_are_each_mapped_whichever_map_is_used_first(bool readOnlyFirst)
    {
        var mapper = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Service, ServiceDto>();
            cfg.CreateMap<Basket, ReadOnlyBasketDto>().ForMember(d => d.Services, o => o.MapAtRuntime());
            cfg.CreateMap<Basket, BasketDto>().ForMember(d => d.Services, o => o.MapAtRuntime());
        }).CreateMapper();
        void ReadOnly() => mapper.Map<ReadOnlyBasketDto>(TwoServices()).Services.Select(service => service.Description).ShouldBe(["one", "two"]);
        void Writable() => mapper.Map<BasketDto>(TwoServices()).Services.Select(service => service.Description).ShouldBe(["one", "two"]);

        // Before: with the plan built for the member without a setter, which fills the collection it is given, the
        // member with a setter failed with a NullReferenceException inside an AutoMapperMappingException.
        if (readOnlyFirst)
        {
            ReadOnly();
            Writable();
        }
        else
        {
            Writable();
            ReadOnly();
        }
        ReadOnly();
        Writable();
    }

    static Basket TwoServices() => new() { Services = [new() { Description = "one" }, new() { Description = "two" }] };

    // Null sources and value transformers are handled where the member is mapped, in the plan of its map, and never
    // were shared. These pin that.

    class NullAllowedBasketDto
    {
        public List<ServiceDto> Services { get; set; }
    }
    class NullNotAllowedBasketDto
    {
        public List<ServiceDto> Services { get; set; }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AllowNull_is_the_setting_of_the_member_being_mapped_whichever_map_is_used_first(bool allowNullFirst)
    {
        var mapper = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Service, ServiceDto>();
            cfg.CreateMap<Basket, NullAllowedBasketDto>().ForMember(d => d.Services, o =>
            {
                o.AllowNull();
                o.MapAtRuntime();
            });
            cfg.CreateMap<Basket, NullNotAllowedBasketDto>().ForMember(d => d.Services, o => o.MapAtRuntime());
        }).CreateMapper();
        void Allowed()
        {
            mapper.Map<NullAllowedBasketDto>(TwoServices()).Services.Count.ShouldBe(2);
            mapper.Map<NullAllowedBasketDto>(new Basket()).Services.ShouldBeNull();
        }
        void NotAllowed()
        {
            mapper.Map<NullNotAllowedBasketDto>(TwoServices()).Services.Count.ShouldBe(2);
            mapper.Map<NullNotAllowedBasketDto>(new Basket()).Services.ShouldBeEmpty();
        }

        if (allowNullFirst)
        {
            Allowed();
            NotAllowed();
        }
        else
        {
            NotAllowed();
            Allowed();
        }
        Allowed();
        NotAllowed();
    }

    class NullCollectionsProfile : Profile
    {
        public NullCollectionsProfile()
        {
            AllowNullCollections = true;
            CreateMap<Basket, NullAllowedBasketDto>().ForMember(d => d.Services, o => o.MapAtRuntime());
        }
    }
    class EmptyCollectionsProfile : Profile
    {
        public EmptyCollectionsProfile() => CreateMap<Basket, NullNotAllowedBasketDto>().ForMember(d => d.Services, o => o.MapAtRuntime());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AllowNullCollections_is_the_setting_of_the_profile_of_the_member_being_mapped_whichever_map_is_used_first(bool nullCollectionsFirst)
    {
        var mapper = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Service, ServiceDto>();
            cfg.AddProfile<NullCollectionsProfile>();
            cfg.AddProfile<EmptyCollectionsProfile>();
        }).CreateMapper();
        void NullCollections()
        {
            mapper.Map<NullAllowedBasketDto>(TwoServices()).Services.Count.ShouldBe(2);
            mapper.Map<NullAllowedBasketDto>(new Basket()).Services.ShouldBeNull();
        }
        void EmptyCollections()
        {
            mapper.Map<NullNotAllowedBasketDto>(TwoServices()).Services.Count.ShouldBe(2);
            mapper.Map<NullNotAllowedBasketDto>(new Basket()).Services.ShouldBeEmpty();
        }

        if (nullCollectionsFirst)
        {
            NullCollections();
            EmptyCollections();
        }
        else
        {
            EmptyCollections();
            NullCollections();
        }
        NullCollections();
        EmptyCollections();
    }

    class Label
    {
        public string Text { get; set; }
    }
    class ShoutedLabelDto
    {
        public string Text { get; set; }
    }
    class QuietLabelDto
    {
        public string Text { get; set; }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Value_transformers_are_those_of_the_member_being_mapped_whichever_map_is_used_first(bool transformedFirst)
    {
        var mapper = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Label, ShoutedLabelDto>().ForMember(d => d.Text, o =>
            {
                o.AddTransform(text => text + "!");
                o.MapAtRuntime();
            });
            cfg.CreateMap<Label, QuietLabelDto>().ForMember(d => d.Text, o => o.MapAtRuntime());
        }).CreateMapper();
        void Shouted() => mapper.Map<ShoutedLabelDto>(new Label { Text = "text" }).Text.ShouldBe("text!");
        void Quiet() => mapper.Map<QuietLabelDto>(new Label { Text = "text" }).Text.ShouldBe("text");

        if (transformedFirst)
        {
            Shouted();
            Quiet();
        }
        else
        {
            Quiet();
            Shouted();
        }
        Shouted();
        Quiet();
    }

    // CompileMappings compiles the plans before the first Map call, the plan of such a member too.

    class Token { }
    class TokenDto { }
    class Wallet
    {
        public Token Token { get; set; }
    }
    class WalletDto
    {
        public TokenDto Token { get; set; }
    }
    // Maps Token to TokenDto, and records the member each of its plans is built for.
    sealed class RecordingMapper : IObjectMapper
    {
        public List<MemberMap> BuiltFor { get; } = [];
        public bool IsMatch(TypePair context) => context == new TypePair(typeof(Token), typeof(TokenDto));
        public Expression MapExpression(IGlobalConfiguration configuration, ProfileMap profileMap, MemberMap memberMap, Expression sourceExpression, Expression destExpression)
        {
            BuiltFor.Add(memberMap);
            return Expression.New(typeof(TokenDto));
        }
    }

    [Fact]
    public void CompileMappings_builds_the_plan_of_a_member_that_is_mapped_through_the_context()
    {
        RecordingMapper recorder = new();
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.Internal().Mappers.Insert(0, recorder);
            cfg.CreateMap<Wallet, WalletDto>().ForMember(d => d.Token, o => o.MapAtRuntime());
        });

        configuration.CompileMappings();

        // Before: CompileMappings built one plan for the two types, and the member ran that one. Now that the member
        // has a plan of its own, CompileMappings builds it as well: the first Map call still builds nothing.
        var member = configuration.Internal().FindTypeMapFor<Wallet, WalletDto>().PropertyMaps.Single();
        recorder.BuiltFor.ShouldContain(member);
        var plansBuilt = recorder.BuiltFor.Count;
        configuration.CreateMapper().Map<WalletDto>(new Wallet { Token = new() }).Token.ShouldNotBeNull();
        recorder.BuiltFor.Count.ShouldBe(plansBuilt);
    }

    // What is still shared, so that a configuration compiles no more plans than it needs.

    [Fact]
    public void Map_calls_without_a_member_share_one_plan_per_pair_of_types()
    {
        var configuration = MaxDepthConfiguration(NotInlinedBecause.MapAtRuntime).Internal();
        TypePair lines = new(typeof(PlainLine), typeof(PlainLineDto));
        TypePair services = new(typeof(List<Service>), typeof(IList<ServiceDto>));

        // A request made with the types alone is what CompileMappings and BuildExecutionPlan make; a Map call passes
        // no member.
        configuration.GetExecutionPlan<PlainLine, PlainLineDto>(new(lines, lines, null))
            .ShouldBeSameAs(configuration.GetExecutionPlan<PlainLine, PlainLineDto>(new(lines)));
        configuration.GetExecutionPlan<List<Service>, IList<ServiceDto>>(new(services, services, null))
            .ShouldBeSameAs(configuration.GetExecutionPlan<List<Service>, IList<ServiceDto>>(new(services)));
        new MapRequest(lines, lines, null).ShouldBe(new MapRequest(lines));
        new MapRequest(lines, lines, null).GetHashCode().ShouldBe(new MapRequest(lines).GetHashCode());
    }

    [Fact]
    public void The_plan_of_a_type_map_is_compiled_once_whichever_member_asks_for_it()
    {
        var configuration = MaxDepthConfiguration(NotInlinedBecause.MapAtRuntime).Internal();
        TypePair lines = new(typeof(PlainLine), typeof(PlainLineDto));
        var attachment = configuration.FindTypeMapFor<RecursiveLine, RecursiveLineDto>().PropertyMaps.Single(member => member.DestinationName == nameof(RecursiveLineDto.Attachment));
        var parent = configuration.FindTypeMapFor<RecursiveLine, RecursiveLineDto>().PropertyMaps.Single(member => member.DestinationName == nameof(RecursiveLineDto.Parent));

        // The plan of a type map does not depend on the member it is mapped for.
        var withoutMember = configuration.GetExecutionPlan<PlainLine, PlainLineDto>(new(lines, lines, null));
        configuration.GetExecutionPlan<PlainLine, PlainLineDto>(new(lines, lines, attachment)).ShouldBeSameAs(withoutMember);
        configuration.GetExecutionPlan<PlainLine, PlainLineDto>(new(lines, lines, parent)).ShouldBeSameAs(withoutMember);
    }

    [Fact]
    public void Requests_for_different_members_are_different_requests()
    {
        var configuration = MaxDepthConfiguration(NotInlinedBecause.MapAtRuntime).Internal();
        TypePair services = new(typeof(List<Service>), typeof(IList<ServiceDto>));
        var recursive = configuration.FindTypeMapFor<RecursiveLine, RecursiveLineDto>().PropertyMaps.Single(member => member.DestinationName == nameof(RecursiveLineDto.Services));
        var plain = configuration.FindTypeMapFor<PlainLine, PlainLineDto>().PropertyMaps.Single();

        // Before: equal, the member was not compared.
        new MapRequest(services, services, recursive).ShouldNotBe(new MapRequest(services, services, plain));
        new MapRequest(services, services, recursive).ShouldNotBe(new MapRequest(services, services, null));
        new MapRequest(services, services, recursive).ShouldBe(new MapRequest(services, services, recursive));
        new MapRequest(services, services, recursive).GetHashCode().ShouldBe(new MapRequest(services, services, recursive).GetHashCode());
    }
}
