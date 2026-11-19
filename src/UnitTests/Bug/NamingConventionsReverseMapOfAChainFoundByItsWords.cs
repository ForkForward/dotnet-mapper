namespace ForkForward.Mapper.UnitTests.Bug.NamingConventions;

// A destination member whose words are a chain of three source members or more, across two naming conventions, is now matched
// to that chain (see FlatteningOverThreeLevels). ReverseMap() reverses the source members a member was matched to, and it did
// so also for a member whose own configuration maps it from somewhere else, or ignores it. Such a member had no source members
// before, and its reverse map wrote nothing to the chain; with the chain it wrote the value of the member there as well,
// creating the objects on the way and overwriting the value an existing object had. Found in review. A chain that only the
// new reading finds is now reversed only for a member that is mapped through it; a chain that was found before is reversed
// as before.
public class ReverseMapOfAChainFoundByItsWords
{
    class Source
    {
        public string nickname { get; set; }
        public Customer customer { get; set; }
    }
    class Customer
    {
        public string name { get; set; }
        public Address address { get; set; }
    }
    class Address
    {
        public City city { get; set; }
    }
    class City
    {
        public string name { get; set; }
    }
    class Destination
    {
        public string CustomerAddressCityName { get; set; }
    }
    class DestinationOfTwo
    {
        public string CustomerName { get; set; }
    }
    class Upper : IValueConverter<string, string>
    {
        public string Convert(string sourceMember, ResolutionContext context) => sourceMember?.ToUpperInvariant();
    }

    static IMapper Mapper(Action<IMapperConfigurationExpression> createMaps) => new MapperConfiguration(cfg =>
    {
        cfg.SourceMemberNamingConvention = LowerUnderscoreNamingConvention.Instance;
        cfg.DestinationMemberNamingConvention = PascalCaseNamingConvention.Instance;
        createMaps(cfg);
    }).CreateMapper();

    static Source InParis() => new() { nickname = "old", customer = new() { name = "Ann", address = new() { city = new() { name = "Paris" } } } };

    [Fact]
    public void A_member_mapped_from_somewhere_else_is_not_written_back_to_the_chain()
    {
        var mapper = Mapper(cfg => cfg.CreateMap<Source, Destination>()
            .ForMember(d => d.CustomerAddressCityName, o => o.MapFrom(s => s.nickname))
            .ReverseMap());

        mapper.Map<Destination>(InParis()).CustomerAddressCityName.ShouldBe("old");

        var existing = InParis();
        mapper.Map(new Destination { CustomerAddressCityName = "new" }, existing);
        // The reverse of the MapFrom, and nothing else. Before the fix the city was renamed to "new".
        existing.nickname.ShouldBe("new");
        existing.customer.address.city.name.ShouldBe("Paris");

        var created = mapper.Map<Source>(new Destination { CustomerAddressCityName = "new" });
        created.nickname.ShouldBe("new");
        // Before the fix the customer, its address and its city were created to hold the value.
        created.customer.ShouldBeNull();
    }

    [Fact]
    public void An_ignored_member_is_not_written_back_to_the_chain()
    {
        var mapper = Mapper(cfg =>
        {
            var map = cfg.CreateMap<Source, Destination>();
            map.ForAllMembers(o => o.Ignore());
            map.ReverseMap();
        });

        mapper.Map<Destination>(InParis()).CustomerAddressCityName.ShouldBeNull();
        var existing = InParis();
        mapper.Map(new Destination { CustomerAddressCityName = "new" }, existing);
        // Before the fix the city was renamed to "new".
        existing.customer.address.city.name.ShouldBe("Paris");
        mapper.Map<Source>(new Destination { CustomerAddressCityName = "new" }).customer.ShouldBeNull();
    }

    [Fact]
    public void A_member_mapped_through_the_chain_is_written_back_to_it()
    {
        var mapper = Mapper(cfg => cfg.CreateMap<Source, Destination>().ReverseMap());

        mapper.Map<Destination>(InParis()).CustomerAddressCityName.ShouldBe("Paris");
        var existing = InParis();
        mapper.Map(new Destination { CustomerAddressCityName = "Rome" }, existing);
        existing.customer.address.city.name.ShouldBe("Rome");
        mapper.Map<Source>(new Destination { CustomerAddressCityName = "Rome" }).customer.address.city.name.ShouldBe("Rome");
    }

    [Fact]
    public void A_member_converted_from_the_chain_is_written_back_to_it_as_a_chain_of_two_is()
    {
        var mapper = Mapper(cfg => cfg.CreateMap<Source, Destination>()
            .ForMember(d => d.CustomerAddressCityName, o => o.ConvertUsing(new Upper()))
            .ReverseMap());

        mapper.Map<Destination>(InParis()).CustomerAddressCityName.ShouldBe("PARIS");
        var existing = InParis();
        mapper.Map(new Destination { CustomerAddressCityName = "Rome" }, existing);
        // The value converter reads the chain, so the chain is the member's source, and the reverse map writes the
        // member's value to it, unconverted, as it does for a chain of two.
        existing.customer.address.city.name.ShouldBe("Rome");
    }

    [Fact]
    public void A_chain_of_two_that_was_matched_before_is_written_back_as_before_also_when_the_member_is_mapped_from_somewhere_else()
    {
        // The behaviour of 14.1.1 and of AutoMapper 14, kept: the chain customer.name was found by the search that existed
        // before, and the reverse map writes the member's value both to where MapFrom reads it and to that chain. Whether that
        // should change is an open question in ROADMAP.md.
        var mapper = Mapper(cfg => cfg.CreateMap<Source, DestinationOfTwo>()
            .ForMember(d => d.CustomerName, o => o.MapFrom(s => s.nickname))
            .ReverseMap());

        var existing = InParis();
        mapper.Map(new DestinationOfTwo { CustomerName = "new" }, existing);
        existing.nickname.ShouldBe("new");
        existing.customer.name.ShouldBe("new");
    }
}
