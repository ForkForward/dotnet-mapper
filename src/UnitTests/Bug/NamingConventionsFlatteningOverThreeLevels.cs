namespace ForkForward.Mapper.UnitTests.Bug.NamingConventions;

// With different naming conventions on the two sides, a destination member is matched to a chain of source members by
// its words: the first words name a source member, and what is left of the name is looked for in the type of that
// member. What was left used to be put together with the separator of the source side and split again with the
// convention of the destination side, which cannot split it: Address_City_Name is not PascalCase and addresscityname
// has no underscore. So a chain of two members was found and a chain of three was not, in either direction.
// When that search finds nothing, the words of the destination member are now followed as they were split, at any depth.
// What was matched before is matched the same way: the old search is made first.
public class FlatteningOverThreeLevels
{
    class SnakeOrder
    {
        public decimal total { get; set; }
        public SnakeCustomer customer { get; set; }
    }
    class SnakeCustomer
    {
        public string name { get; set; }
        public SnakeAddress address { get; set; }
        public SnakeAddress home_address { get; set; }
    }
    class SnakeAddress
    {
        public string street { get; set; }
        public SnakeCity city { get; set; }
    }
    class SnakeCity
    {
        public string name { get; set; }
        public string zip_code { get; set; }
        public SnakeCountry country { get; set; }
    }
    class SnakeCountry
    {
        public string iso_code { get; set; }
    }
    class PascalOrderDto
    {
        public decimal Total { get; set; }
        public string CustomerName { get; set; }
        public string CustomerAddressStreet { get; set; }
        public string CustomerAddressCityName { get; set; }
        public string CustomerHomeAddressCityZipCode { get; set; }
        public string CustomerAddressCityCountryIsoCode { get; set; }
    }
    class PascalOrder
    {
        public decimal Total { get; set; }
        public PascalCustomer Customer { get; set; }
    }
    class PascalCustomer
    {
        public string Name { get; set; }
        public PascalAddress Address { get; set; }
        public PascalAddress HomeAddress { get; set; }
    }
    class PascalAddress
    {
        public string Street { get; set; }
        public PascalCity City { get; set; }
    }
    class PascalCity
    {
        public string Name { get; set; }
        public string ZipCode { get; set; }
        public PascalCountry Country { get; set; }
    }
    class PascalCountry
    {
        public string IsoCode { get; set; }
    }
    class SnakeOrderDto
    {
        public decimal total { get; set; }
        public string customer_name { get; set; }
        public string customer_address_street { get; set; }
        public string customer_address_city_name { get; set; }
        public string customer_home_address_city_zip_code { get; set; }
        public string customer_address_city_country_iso_code { get; set; }
    }
    static void LowerUnderscoreToPascalCase(IProfileExpression profile)
    {
        profile.SourceMemberNamingConvention = LowerUnderscoreNamingConvention.Instance;
        profile.DestinationMemberNamingConvention = PascalCaseNamingConvention.Instance;
    }
    static void PascalCaseToLowerUnderscore(IProfileExpression profile)
    {
        profile.SourceMemberNamingConvention = PascalCaseNamingConvention.Instance;
        profile.DestinationMemberNamingConvention = LowerUnderscoreNamingConvention.Instance;
    }
    // the chain of source members a destination member is mapped from, null when it was not matched
    static string SourceMembers<TSource, TDestination>(MapperConfiguration configuration, string destinationMember) =>
        configuration.FindTypeMapFor<TSource, TDestination>().PropertyMaps.SingleOrDefault(propertyMap => propertyMap.DestinationName == destinationMember) is { } propertyMap ?
            string.Join('.', propertyMap.SourceMembers.Select(member => member.Name)) : null;
    static SnakeOrder CreateSnakeOrder() => new()
    {
        total = 12.5m,
        customer = new()
        {
            name = "customer",
            address = new() { street = "street", city = new() { name = "city", zip_code = "zip code", country = new() { iso_code = "PL" } } },
            home_address = new() { street = "home street", city = new() { name = "home city", zip_code = "home zip code" } }
        }
    };
    static PascalOrder CreatePascalOrder() => new()
    {
        Total = 12.5m,
        Customer = new()
        {
            Name = "customer",
            Address = new() { Street = "street", City = new() { Name = "city", ZipCode = "zip code", Country = new() { IsoCode = "PL" } } },
            HomeAddress = new() { Street = "home street", City = new() { Name = "home city", ZipCode = "home zip code" } }
        }
    };
    static void ShouldBeFlattened(PascalOrderDto dto)
    {
        dto.Total.ShouldBe(12.5m);
        dto.CustomerName.ShouldBe("customer");
        dto.CustomerAddressStreet.ShouldBe("street");
        dto.CustomerAddressCityName.ShouldBe("city");
        dto.CustomerHomeAddressCityZipCode.ShouldBe("home zip code");
        dto.CustomerAddressCityCountryIsoCode.ShouldBe("PL");
    }
    static void ShouldBeFlattened(SnakeOrderDto dto)
    {
        dto.total.ShouldBe(12.5m);
        dto.customer_name.ShouldBe("customer");
        dto.customer_address_street.ShouldBe("street");
        dto.customer_address_city_name.ShouldBe("city");
        dto.customer_home_address_city_zip_code.ShouldBe("home zip code");
        dto.customer_address_city_country_iso_code.ShouldBe("PL");
    }
    static void ShouldBeUnflattened(SnakeOrder order)
    {
        order.total.ShouldBe(12.5m);
        order.customer.name.ShouldBe("customer");
        order.customer.address.street.ShouldBe("street");
        order.customer.address.city.name.ShouldBe("city");
        order.customer.home_address.city.zip_code.ShouldBe("home zip code");
        order.customer.address.city.country.iso_code.ShouldBe("PL");
    }
    static void ShouldBeUnflattened(PascalOrder order)
    {
        order.Total.ShouldBe(12.5m);
        order.Customer.Name.ShouldBe("customer");
        order.Customer.Address.Street.ShouldBe("street");
        order.Customer.Address.City.Name.ShouldBe("city");
        order.Customer.HomeAddress.City.ZipCode.ShouldBe("home zip code");
        order.Customer.Address.City.Country.IsoCode.ShouldBe("PL");
    }

    // one member, a chain of two (matched before), and chains of three, four and five
    [Theory]
    [InlineData(nameof(PascalOrderDto.Total), "total")]
    [InlineData(nameof(PascalOrderDto.CustomerName), "customer.name")]
    [InlineData(nameof(PascalOrderDto.CustomerAddressStreet), "customer.address.street")]
    [InlineData(nameof(PascalOrderDto.CustomerAddressCityName), "customer.address.city.name")]
    [InlineData(nameof(PascalOrderDto.CustomerHomeAddressCityZipCode), "customer.home_address.city.zip_code")]
    [InlineData(nameof(PascalOrderDto.CustomerAddressCityCountryIsoCode), "customer.address.city.country.iso_code")]
    public void A_pascal_case_member_from_a_chain_of_lower_underscore_members(string destinationMember, string sourceMembers)
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            LowerUnderscoreToPascalCase(cfg);
            cfg.CreateMap<SnakeOrder, PascalOrderDto>();
        });

        SourceMembers<SnakeOrder, PascalOrderDto>(configuration, destinationMember).ShouldBe(sourceMembers);
    }
    [Theory]
    [InlineData(nameof(SnakeOrderDto.total), "Total")]
    [InlineData(nameof(SnakeOrderDto.customer_name), "Customer.Name")]
    [InlineData(nameof(SnakeOrderDto.customer_address_street), "Customer.Address.Street")]
    [InlineData(nameof(SnakeOrderDto.customer_address_city_name), "Customer.Address.City.Name")]
    [InlineData(nameof(SnakeOrderDto.customer_home_address_city_zip_code), "Customer.HomeAddress.City.ZipCode")]
    [InlineData(nameof(SnakeOrderDto.customer_address_city_country_iso_code), "Customer.Address.City.Country.IsoCode")]
    public void A_lower_underscore_member_from_a_chain_of_pascal_case_members(string destinationMember, string sourceMembers)
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            PascalCaseToLowerUnderscore(cfg);
            cfg.CreateMap<PascalOrder, SnakeOrderDto>();
        });

        SourceMembers<PascalOrder, SnakeOrderDto>(configuration, destinationMember).ShouldBe(sourceMembers);
    }
    [Fact]
    public void Lower_underscore_to_pascal_case()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            LowerUnderscoreToPascalCase(cfg);
            cfg.CreateMap<SnakeOrder, PascalOrderDto>();
        });
        configuration.AssertConfigurationIsValid();

        ShouldBeFlattened(configuration.CreateMapper().Map<PascalOrderDto>(CreateSnakeOrder()));
    }
    [Fact]
    public void Pascal_case_to_lower_underscore()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            PascalCaseToLowerUnderscore(cfg);
            cfg.CreateMap<PascalOrder, SnakeOrderDto>();
        });
        configuration.AssertConfigurationIsValid();

        ShouldBeFlattened(configuration.CreateMapper().Map<SnakeOrderDto>(CreatePascalOrder()));
    }
    [Fact]
    public void A_null_on_the_way_gives_no_value()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            LowerUnderscoreToPascalCase(cfg);
            cfg.CreateMap<SnakeOrder, PascalOrderDto>();
        });
        configuration.AssertConfigurationIsValid();
        var mapper = configuration.CreateMapper();

        var dto = mapper.Map<PascalOrderDto>(new SnakeOrder { customer = new() { name = "customer", address = new() { street = "street" } } });
        dto.CustomerName.ShouldBe("customer");
        dto.CustomerAddressStreet.ShouldBe("street");
        dto.CustomerAddressCityName.ShouldBeNull();
        dto.CustomerHomeAddressCityZipCode.ShouldBeNull();
        dto.CustomerAddressCityCountryIsoCode.ShouldBeNull();
        mapper.Map<PascalOrderDto>(new SnakeOrder()).CustomerAddressCityCountryIsoCode.ShouldBeNull();
    }
    [Fact]
    public void A_member_that_is_configured_or_ignored_stays_as_it_was()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            LowerUnderscoreToPascalCase(cfg);
            cfg.CreateMap<SnakeOrder, PascalStreetAndCityNameDto>()
                .ForMember(dto => dto.CustomerAddressCityName, options => options.MapFrom(order => order.customer.name))
                .ForMember(dto => dto.CustomerAddressStreet, options => options.Ignore());
        });
        configuration.AssertConfigurationIsValid();

        var dto = configuration.CreateMapper().Map<PascalStreetAndCityNameDto>(CreateSnakeOrder());
        dto.CustomerAddressCityName.ShouldBe("customer");
        dto.CustomerAddressStreet.ShouldBeNull();
    }

    [Fact]
    public void ReverseMap_unflattens_into_lower_underscore()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            LowerUnderscoreToPascalCase(cfg);
            cfg.CreateMap<SnakeOrder, PascalOrderDto>().ReverseMap();
        });
        configuration.AssertConfigurationIsValid();
        var mapper = configuration.CreateMapper();

        var dto = mapper.Map<PascalOrderDto>(CreateSnakeOrder());
        ShouldBeFlattened(dto);
        ShouldBeUnflattened(mapper.Map<SnakeOrder>(dto));
    }
    [Fact]
    public void ReverseMap_unflattens_into_pascal_case()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            PascalCaseToLowerUnderscore(cfg);
            cfg.CreateMap<PascalOrder, SnakeOrderDto>().ReverseMap();
        });
        configuration.AssertConfigurationIsValid();
        var mapper = configuration.CreateMapper();

        var dto = mapper.Map<SnakeOrderDto>(CreatePascalOrder());
        ShouldBeFlattened(dto);
        ShouldBeUnflattened(mapper.Map<PascalOrder>(dto));
    }
    [Fact]
    public void A_reverse_map_that_flattens_into_pascal_case()
    {
        // the conventions are those of the map it reverses, the other way round
        var configuration = new MapperConfiguration(cfg =>
        {
            PascalCaseToLowerUnderscore(cfg);
            cfg.CreateMap<PascalOrderDto, SnakeOrder>(MemberList.None).ReverseMap();
        });
        configuration.AssertConfigurationIsValid();

        SourceMembers<SnakeOrder, PascalOrderDto>(configuration, nameof(PascalOrderDto.CustomerName)).ShouldBe("customer.name");
        SourceMembers<SnakeOrder, PascalOrderDto>(configuration, nameof(PascalOrderDto.CustomerAddressCityName)).ShouldBe("customer.address.city.name");
        SourceMembers<SnakeOrder, PascalOrderDto>(configuration, nameof(PascalOrderDto.CustomerHomeAddressCityZipCode)).ShouldBe("customer.home_address.city.zip_code");
        ShouldBeFlattened(configuration.CreateMapper().Map<PascalOrderDto>(CreateSnakeOrder()));
    }
    [Fact]
    public void A_reverse_map_that_flattens_into_lower_underscore()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            LowerUnderscoreToPascalCase(cfg);
            cfg.CreateMap<SnakeOrderDto, PascalOrder>(MemberList.None).ReverseMap();
        });
        configuration.AssertConfigurationIsValid();

        SourceMembers<PascalOrder, SnakeOrderDto>(configuration, nameof(SnakeOrderDto.customer_name)).ShouldBe("Customer.Name");
        SourceMembers<PascalOrder, SnakeOrderDto>(configuration, nameof(SnakeOrderDto.customer_address_city_name)).ShouldBe("Customer.Address.City.Name");
        SourceMembers<PascalOrder, SnakeOrderDto>(configuration, nameof(SnakeOrderDto.customer_home_address_city_zip_code)).ShouldBe("Customer.HomeAddress.City.ZipCode");
        ShouldBeFlattened(configuration.CreateMapper().Map<SnakeOrderDto>(CreatePascalOrder()));
    }

    [Fact]
    public void A_convention_of_a_profile_and_a_convention_of_the_configuration()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            LowerUnderscoreToPascalCase(cfg);
            cfg.CreateMap<SnakeOrder, PascalOrderDto>();
            cfg.CreateProfile("TheConventionsOfTheConfiguration", profile => profile.CreateMap<SnakeOrder, PascalWithConstructor>());
            cfg.CreateProfile("ItsOwnConventions", profile =>
            {
                PascalCaseToLowerUnderscore(profile);
                profile.CreateMap<PascalOrder, SnakeOrderDto>().ReverseMap();
            });
            cfg.CreateProfile("TheDefaultConventions", profile =>
            {
                profile.SourceMemberNamingConvention = PascalCaseNamingConvention.Instance;
                profile.CreateMap<PascalOrder, PascalOrderDto>();
            });
        });
        configuration.AssertConfigurationIsValid();
        var mapper = configuration.CreateMapper();

        ShouldBeFlattened(mapper.Map<PascalOrderDto>(CreateSnakeOrder()));
        mapper.Map<PascalWithConstructor>(CreateSnakeOrder()).CustomerAddressCityName.ShouldBe("city");
        var dto = mapper.Map<SnakeOrderDto>(CreatePascalOrder());
        ShouldBeFlattened(dto);
        ShouldBeUnflattened(mapper.Map<PascalOrder>(dto));
        ShouldBeFlattened(mapper.Map<PascalOrderDto>(CreatePascalOrder()));
    }
    [Fact]
    public void A_convention_of_a_profile_only()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.CreateProfile("LowerUnderscoreToPascalCase", profile =>
            {
                LowerUnderscoreToPascalCase(profile);
                profile.CreateMap<SnakeOrder, PascalOrderDto>();
            });
            cfg.CreateProfile("LowerUnderscoreDestinationOnly", profile =>
            {
                profile.DestinationMemberNamingConvention = LowerUnderscoreNamingConvention.Instance;
                profile.CreateMap<PascalOrder, SnakeOrderDto>();
            });
            cfg.CreateMap<PascalOrder, PascalOrderDto>();
        });
        configuration.AssertConfigurationIsValid();
        var mapper = configuration.CreateMapper();

        ShouldBeFlattened(mapper.Map<PascalOrderDto>(CreateSnakeOrder()));
        ShouldBeFlattened(mapper.Map<SnakeOrderDto>(CreatePascalOrder()));
        ShouldBeFlattened(mapper.Map<PascalOrderDto>(CreatePascalOrder()));
    }

    class SnakeRuns
    {
        public SnakeRunsCustomer customer { get; set; }
        public SnakeRunsCustomer vip_customer { get; set; }
    }
    class SnakeRunsCustomer
    {
        public SnakeRunsAddress address { get; set; }
        public SnakeRunsAddress xml_address { get; set; }
    }
    class SnakeRunsAddress
    {
        public SnakeRunsCity city { get; set; }
    }
    class SnakeRunsCity
    {
        public int id { get; set; }
        public string name { get; set; }
    }
    class PascalRunsDto
    {
        public int CustomerAddressCityID { get; set; }
        public string CustomerXMLAddressCityName { get; set; }
        public string VIPCustomerAddressCityName { get; set; }
        public int VIPCustomerXMLAddressCityID { get; set; }
    }
    class PascalRuns
    {
        public PascalRunsCustomer Customer { get; set; }
        public PascalRunsCustomer VIPCustomer { get; set; }
    }
    class PascalRunsCustomer
    {
        public PascalRunsAddress Address { get; set; }
        public PascalRunsAddress XMLAddress { get; set; }
    }
    class PascalRunsAddress
    {
        public PascalRunsCity City { get; set; }
    }
    class PascalRunsCity
    {
        public int ID { get; set; }
        public string Name { get; set; }
    }
    class SnakeRunsDto
    {
        public int customer_address_city_id { get; set; }
        public string customer_xml_address_city_name { get; set; }
        public string vip_customer_address_city_name { get; set; }
        public int vip_customer_xml_address_city_id { get; set; }
    }
    [Fact]
    public void A_run_of_capitals_in_a_chain_of_lower_underscore_members()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            LowerUnderscoreToPascalCase(cfg);
            cfg.CreateMap<SnakeRuns, PascalRunsDto>().ReverseMap();
        });
        configuration.AssertConfigurationIsValid();
        var mapper = configuration.CreateMapper();

        // a run at the end of the name, in the middle, at its start, and all three
        SourceMembers<SnakeRuns, PascalRunsDto>(configuration, nameof(PascalRunsDto.CustomerAddressCityID)).ShouldBe("customer.address.city.id");
        SourceMembers<SnakeRuns, PascalRunsDto>(configuration, nameof(PascalRunsDto.CustomerXMLAddressCityName)).ShouldBe("customer.xml_address.city.name");
        SourceMembers<SnakeRuns, PascalRunsDto>(configuration, nameof(PascalRunsDto.VIPCustomerAddressCityName)).ShouldBe("vip_customer.address.city.name");
        SourceMembers<SnakeRuns, PascalRunsDto>(configuration, nameof(PascalRunsDto.VIPCustomerXMLAddressCityID)).ShouldBe("vip_customer.xml_address.city.id");
        var dto = mapper.Map<PascalRunsDto>(new SnakeRuns
        {
            customer = new() { address = new() { city = new() { id = 1 } }, xml_address = new() { city = new() { name = "xml" } } },
            vip_customer = new() { address = new() { city = new() { name = "vip" } }, xml_address = new() { city = new() { id = 2 } } }
        });
        dto.CustomerAddressCityID.ShouldBe(1);
        dto.CustomerXMLAddressCityName.ShouldBe("xml");
        dto.VIPCustomerAddressCityName.ShouldBe("vip");
        dto.VIPCustomerXMLAddressCityID.ShouldBe(2);

        var source = mapper.Map<SnakeRuns>(dto);
        source.customer.address.city.id.ShouldBe(1);
        source.customer.xml_address.city.name.ShouldBe("xml");
        source.vip_customer.address.city.name.ShouldBe("vip");
        source.vip_customer.xml_address.city.id.ShouldBe(2);
    }
    [Fact]
    public void A_run_of_capitals_in_a_chain_of_pascal_case_members()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            PascalCaseToLowerUnderscore(cfg);
            cfg.CreateMap<PascalRuns, SnakeRunsDto>().ReverseMap();
        });
        configuration.AssertConfigurationIsValid();
        var mapper = configuration.CreateMapper();

        SourceMembers<PascalRuns, SnakeRunsDto>(configuration, nameof(SnakeRunsDto.customer_address_city_id)).ShouldBe("Customer.Address.City.ID");
        SourceMembers<PascalRuns, SnakeRunsDto>(configuration, nameof(SnakeRunsDto.customer_xml_address_city_name)).ShouldBe("Customer.XMLAddress.City.Name");
        SourceMembers<PascalRuns, SnakeRunsDto>(configuration, nameof(SnakeRunsDto.vip_customer_address_city_name)).ShouldBe("VIPCustomer.Address.City.Name");
        SourceMembers<PascalRuns, SnakeRunsDto>(configuration, nameof(SnakeRunsDto.vip_customer_xml_address_city_id)).ShouldBe("VIPCustomer.XMLAddress.City.ID");
        var dto = mapper.Map<SnakeRunsDto>(new PascalRuns
        {
            Customer = new() { Address = new() { City = new() { ID = 1 } }, XMLAddress = new() { City = new() { Name = "xml" } } },
            VIPCustomer = new() { Address = new() { City = new() { Name = "vip" } }, XMLAddress = new() { City = new() { ID = 2 } } }
        });
        dto.customer_address_city_id.ShouldBe(1);
        dto.customer_xml_address_city_name.ShouldBe("xml");
        dto.vip_customer_address_city_name.ShouldBe("vip");
        dto.vip_customer_xml_address_city_id.ShouldBe(2);

        var source = mapper.Map<PascalRuns>(dto);
        source.Customer.Address.City.ID.ShouldBe(1);
        source.Customer.XMLAddress.City.Name.ShouldBe("xml");
        source.VIPCustomer.Address.City.Name.ShouldBe("vip");
        source.VIPCustomer.XMLAddress.City.ID.ShouldBe(2);
    }

    class SnakeShallowAndDeep
    {
        public SnakeCustomer customer { get; set; }
        public SnakeCustomerAddress customer_address { get; set; }
        public string customer_address_street { get; set; }
    }
    class SnakeCustomerAddress
    {
        public string city_name { get; set; }
    }
    class PascalShallowAndDeep
    {
        public PascalCustomer Customer { get; set; }
        public PascalCustomerAddress CustomerAddress { get; set; }
        public string CustomerAddressStreet { get; set; }
    }
    class PascalCustomerAddress
    {
        public string CityName { get; set; }
    }
    class PascalStreetAndCityNameDto
    {
        public string CustomerAddressStreet { get; set; }
        public string CustomerAddressCityName { get; set; }
    }
    class SnakeStreetAndCityNameDto
    {
        public string customer_address_street { get; set; }
        public string customer_address_city_name { get; set; }
    }
    [Fact]
    public void A_member_and_a_chain_of_two_that_were_found_before_win_over_a_deeper_chain()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            LowerUnderscoreToPascalCase(cfg);
            cfg.CreateMap<SnakeShallowAndDeep, PascalStreetAndCityNameDto>();
            cfg.CreateProfile("PascalCaseToLowerUnderscore", profile =>
            {
                PascalCaseToLowerUnderscore(profile);
                profile.CreateMap<PascalShallowAndDeep, SnakeStreetAndCityNameDto>();
            });
        });
        configuration.AssertConfigurationIsValid();
        var mapper = configuration.CreateMapper();

        // customer.address.street and customer.address.city.name are there as well, and their first member has the shorter name
        SourceMembers<SnakeShallowAndDeep, PascalStreetAndCityNameDto>(configuration, nameof(PascalStreetAndCityNameDto.CustomerAddressStreet)).ShouldBe("customer_address_street");
        SourceMembers<SnakeShallowAndDeep, PascalStreetAndCityNameDto>(configuration, nameof(PascalStreetAndCityNameDto.CustomerAddressCityName)).ShouldBe("customer_address.city_name");
        var dto = mapper.Map<PascalStreetAndCityNameDto>(new SnakeShallowAndDeep
        {
            customer = new() { address = new() { street = "three levels", city = new() { name = "four levels" } } },
            customer_address = new() { city_name = "found before" }, customer_address_street = "found before"
        });
        dto.CustomerAddressStreet.ShouldBe("found before");
        dto.CustomerAddressCityName.ShouldBe("found before");
        SourceMembers<PascalShallowAndDeep, SnakeStreetAndCityNameDto>(configuration, nameof(SnakeStreetAndCityNameDto.customer_address_street)).ShouldBe("CustomerAddressStreet");
        SourceMembers<PascalShallowAndDeep, SnakeStreetAndCityNameDto>(configuration, nameof(SnakeStreetAndCityNameDto.customer_address_city_name)).ShouldBe("CustomerAddress.CityName");
        var snakeDto = mapper.Map<SnakeStreetAndCityNameDto>(new PascalShallowAndDeep
        {
            Customer = new() { Address = new() { Street = "three levels", City = new() { Name = "four levels" } } },
            CustomerAddress = new() { CityName = "found before" }, CustomerAddressStreet = "found before"
        });
        snakeDto.customer_address_street.ShouldBe("found before");
        snakeDto.customer_address_city_name.ShouldBe("found before");
    }

    class SnakeTwoDeepChains
    {
        public SnakeCustomer customer { get; set; }
        public SnakeAddress customer_address { get; set; }
    }
    [Fact]
    public void Of_two_chains_that_were_not_found_before_the_one_with_the_shorter_first_member_is_taken()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            LowerUnderscoreToPascalCase(cfg);
            cfg.CreateMap<SnakeTwoDeepChains, PascalStreetAndCityNameDto>();
        });
        configuration.AssertConfigurationIsValid();

        // customer_address.street is a chain of two and was found before; customer.address.street is not looked for
        SourceMembers<SnakeTwoDeepChains, PascalStreetAndCityNameDto>(configuration, nameof(PascalStreetAndCityNameDto.CustomerAddressStreet)).ShouldBe("customer_address.street");
        // customer_address.city.name is a chain of three and was not found before either: the source members are tried
        // in the order they always were, the fewest words first
        SourceMembers<SnakeTwoDeepChains, PascalStreetAndCityNameDto>(configuration, nameof(PascalStreetAndCityNameDto.CustomerAddressCityName)).ShouldBe("customer.address.city.name");
    }

    class SnakeLetters
    {
        public SnakeLettersCustomer customer { get; set; }
    }
    class SnakeLettersCustomer
    {
        public string ab_name { get; set; }
        public SnakeLettersA a { get; set; }
        public SnakeLettersB ab { get; set; }
    }
    class SnakeLettersDeepOnly
    {
        public SnakeLettersDeepOnlyCustomer customer { get; set; }
    }
    class SnakeLettersDeepOnlyCustomer
    {
        public SnakeLettersA a { get; set; }
        public SnakeLettersB ab { get; set; }
    }
    class SnakeLettersA
    {
        public SnakeLettersB b { get; set; }
    }
    class SnakeLettersB
    {
        public string name { get; set; }
    }
    class PascalLettersDto
    {
        public string CustomerABName { get; set; }
    }
    [Fact]
    public void A_run_of_capitals_that_was_found_before_wins_over_a_deeper_chain()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            LowerUnderscoreToPascalCase(cfg);
            cfg.CreateMap<SnakeLetters, PascalLettersDto>();
        });
        configuration.AssertConfigurationIsValid();

        // customer.a.b.name is there as well, with every capital read as the start of a word, which is the reading tried first
        SourceMembers<SnakeLetters, PascalLettersDto>(configuration, nameof(PascalLettersDto.CustomerABName)).ShouldBe("customer.ab_name");
    }
    [Fact]
    public void Every_capital_as_a_word_is_tried_before_a_run_of_capitals_in_a_deeper_chain_too()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            LowerUnderscoreToPascalCase(cfg);
            cfg.CreateMap<SnakeLettersDeepOnly, PascalLettersDto>();
        });
        configuration.AssertConfigurationIsValid();

        // customer.ab.name is there as well
        SourceMembers<SnakeLettersDeepOnly, PascalLettersDto>(configuration, nameof(PascalLettersDto.CustomerABName)).ShouldBe("customer.a.b.name");
    }

    class PascalNoChainDto
    {
        public string CustomerAddressCityName { get; set; }
        public string CustomerAddressPlanetName { get; set; }
        public string CustomerCityName { get; set; }
        public string CustomerAddressCityNameSuffix { get; set; }
        public string AddressCityName { get; set; }
    }
    class SnakeNoChainDto
    {
        public string customer_address_city_name { get; set; }
        public string customer_address_planet_name { get; set; }
        public string customer_city_name { get; set; }
        public string customer_address_city_name_suffix { get; set; }
        public string address_city_name { get; set; }
    }
    [Fact]
    public void A_name_that_is_no_chain_is_not_matched_and_validation_reports_it()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            LowerUnderscoreToPascalCase(cfg);
            cfg.CreateMap<SnakeOrder, PascalNoChainDto>();
        });

        // a member that is not there, a member that is left out, a word after the end of the chain, and a chain that
        // does not start in the source type
        Should.Throw<AutoMapperConfigurationException>(configuration.AssertConfigurationIsValid).Errors.Single().UnmappedPropertyNames.ShouldBe(
        [
            nameof(PascalNoChainDto.CustomerAddressPlanetName), nameof(PascalNoChainDto.CustomerCityName), nameof(PascalNoChainDto.CustomerAddressCityNameSuffix),
            nameof(PascalNoChainDto.AddressCityName)
        ], ignoreOrder: true);
        var dto = configuration.CreateMapper().Map<PascalNoChainDto>(CreateSnakeOrder());
        dto.CustomerAddressCityName.ShouldBe("city");
        dto.CustomerAddressPlanetName.ShouldBeNull();
        dto.CustomerCityName.ShouldBeNull();
        dto.CustomerAddressCityNameSuffix.ShouldBeNull();
        dto.AddressCityName.ShouldBeNull();
    }
    [Fact]
    public void A_lower_underscore_name_that_is_no_chain_is_not_matched_and_validation_reports_it()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            PascalCaseToLowerUnderscore(cfg);
            cfg.CreateMap<PascalOrder, SnakeNoChainDto>();
        });

        Should.Throw<AutoMapperConfigurationException>(configuration.AssertConfigurationIsValid).Errors.Single().UnmappedPropertyNames.ShouldBe(
        [
            nameof(SnakeNoChainDto.customer_address_planet_name), nameof(SnakeNoChainDto.customer_city_name), nameof(SnakeNoChainDto.customer_address_city_name_suffix),
            nameof(SnakeNoChainDto.address_city_name)
        ], ignoreOrder: true);
        var dto = configuration.CreateMapper().Map<SnakeNoChainDto>(CreatePascalOrder());
        dto.customer_address_city_name.ShouldBe("city");
        dto.customer_address_planet_name.ShouldBeNull();
        dto.customer_city_name.ShouldBeNull();
        dto.customer_address_city_name_suffix.ShouldBeNull();
        dto.address_city_name.ShouldBeNull();
    }

    class PascalWithConstructor(string customerName, string customerAddressCityName, string customerHomeAddressCityZipCode)
    {
        public string CustomerName { get; } = customerName;
        public string CustomerAddressCityName { get; } = customerAddressCityName;
        public string CustomerHomeAddressCityZipCode { get; } = customerHomeAddressCityZipCode;
    }
    class SnakeWithConstructor(string customer_name, string customer_address_city_name, string customer_home_address_city_zip_code)
    {
        public string customer_name { get; } = customer_name;
        public string customer_address_city_name { get; } = customer_address_city_name;
        public string customer_home_address_city_zip_code { get; } = customer_home_address_city_zip_code;
    }
    [Fact]
    public void Constructor_parameters()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            LowerUnderscoreToPascalCase(cfg);
            cfg.CreateMap<SnakeOrder, PascalWithConstructor>();
        });
        configuration.AssertConfigurationIsValid();

        var destination = configuration.CreateMapper().Map<PascalWithConstructor>(CreateSnakeOrder());
        destination.CustomerName.ShouldBe("customer");
        destination.CustomerAddressCityName.ShouldBe("city");
        destination.CustomerHomeAddressCityZipCode.ShouldBe("home zip code");
    }
    [Fact]
    public void Lower_underscore_constructor_parameters()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            PascalCaseToLowerUnderscore(cfg);
            cfg.CreateMap<PascalOrder, SnakeWithConstructor>();
        });
        configuration.AssertConfigurationIsValid();

        var destination = configuration.CreateMapper().Map<SnakeWithConstructor>(CreatePascalOrder());
        destination.customer_name.ShouldBe("customer");
        destination.customer_address_city_name.ShouldBe("city");
        destination.customer_home_address_city_zip_code.ShouldBe("home zip code");
    }

    [Fact]
    public void ExactMatchNamingConvention_for_the_destination_matches_no_chain_as_before()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.SourceMemberNamingConvention = LowerUnderscoreNamingConvention.Instance;
            cfg.DestinationMemberNamingConvention = ExactMatchNamingConvention.Instance;
            cfg.CreateMap<SnakeOrder, PascalOrderDto>();
            cfg.CreateProfile("PascalCaseSource", profile =>
            {
                profile.SourceMemberNamingConvention = PascalCaseNamingConvention.Instance;
                profile.CreateMap<PascalOrder, SnakeOrderDto>();
            });
        });

        var errors = Should.Throw<AutoMapperConfigurationException>(configuration.AssertConfigurationIsValid).Errors;
        errors.Length.ShouldBe(2);
        errors.Single(error => error.TypeMap.DestinationType == typeof(PascalOrderDto)).UnmappedPropertyNames.ShouldBe(
        [
            nameof(PascalOrderDto.CustomerName), nameof(PascalOrderDto.CustomerAddressStreet), nameof(PascalOrderDto.CustomerAddressCityName),
            nameof(PascalOrderDto.CustomerHomeAddressCityZipCode), nameof(PascalOrderDto.CustomerAddressCityCountryIsoCode)
        ], ignoreOrder: true);
        errors.Single(error => error.TypeMap.DestinationType == typeof(SnakeOrderDto)).UnmappedPropertyNames.ShouldBe(
        [
            nameof(SnakeOrderDto.customer_name), nameof(SnakeOrderDto.customer_address_street), nameof(SnakeOrderDto.customer_address_city_name),
            nameof(SnakeOrderDto.customer_home_address_city_zip_code), nameof(SnakeOrderDto.customer_address_city_country_iso_code)
        ], ignoreOrder: true);
    }
    [Fact]
    public void ExactMatchNamingConvention_for_the_source_and_a_pascal_case_destination_as_before()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.SourceMemberNamingConvention = ExactMatchNamingConvention.Instance;
            cfg.CreateMap<PascalOrder, PascalOrderDto>();
        });
        configuration.AssertConfigurationIsValid();

        SourceMembers<PascalOrder, PascalOrderDto>(configuration, nameof(PascalOrderDto.CustomerHomeAddressCityZipCode)).ShouldBe("Customer.HomeAddress.City.ZipCode");
        ShouldBeFlattened(configuration.CreateMapper().Map<PascalOrderDto>(CreatePascalOrder()));
    }
    [Fact]
    public void ExactMatchNamingConvention_for_the_source_and_a_lower_underscore_destination()
    {
        // the words of the destination member are put together with nothing between them, as they were for a chain of two
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.SourceMemberNamingConvention = ExactMatchNamingConvention.Instance;
            cfg.DestinationMemberNamingConvention = LowerUnderscoreNamingConvention.Instance;
            cfg.CreateMap<PascalOrder, SnakeOrderDto>();
        });
        configuration.AssertConfigurationIsValid();

        SourceMembers<PascalOrder, SnakeOrderDto>(configuration, nameof(SnakeOrderDto.customer_name)).ShouldBe("Customer.Name");
        SourceMembers<PascalOrder, SnakeOrderDto>(configuration, nameof(SnakeOrderDto.customer_home_address_city_zip_code)).ShouldBe("Customer.HomeAddress.City.ZipCode");
        ShouldBeFlattened(configuration.CreateMapper().Map<SnakeOrderDto>(CreatePascalOrder()));
    }

    class DoubleUnderscore : INamingConvention
    {
        public string SeparatorCharacter => "__";
        public string[] Split(string input) => input.Split("__", StringSplitOptions.RemoveEmptyEntries);
    }
    class DoubleUnderscoreOrder
    {
        public DoubleUnderscoreCustomer customer { get; set; }
    }
    class DoubleUnderscoreCustomer
    {
        public DoubleUnderscoreAddress home__address { get; set; }
    }
    class DoubleUnderscoreAddress
    {
        public DoubleUnderscoreCity city { get; set; }
    }
    class DoubleUnderscoreCity
    {
        public string zip__code { get; set; }
    }
    class PascalZipCodeDto
    {
        public string CustomerHomeAddressCityZipCode { get; set; }
    }
    class SnakeZipCodeDto
    {
        public string customer_home_address_city_zip_code { get; set; }
    }
    class DoubleUnderscoreZipCodeDto
    {
        public string customer__home__address__city__zip__code { get; set; }
    }
    static DoubleUnderscoreOrder CreateDoubleUnderscoreOrder() => new() { customer = new() { home__address = new() { city = new() { zip__code = "zip code" } } } };
    [Fact]
    public void A_custom_convention_for_the_source()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.SourceMemberNamingConvention = new DoubleUnderscore();
            cfg.CreateMap<DoubleUnderscoreOrder, PascalZipCodeDto>();
        });
        configuration.AssertConfigurationIsValid();

        SourceMembers<DoubleUnderscoreOrder, PascalZipCodeDto>(configuration, nameof(PascalZipCodeDto.CustomerHomeAddressCityZipCode)).ShouldBe("customer.home__address.city.zip__code");
        configuration.CreateMapper().Map<PascalZipCodeDto>(CreateDoubleUnderscoreOrder()).CustomerHomeAddressCityZipCode.ShouldBe("zip code");
    }
    [Fact]
    public void A_custom_convention_for_the_destination()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.DestinationMemberNamingConvention = new DoubleUnderscore();
            cfg.CreateMap<PascalOrder, DoubleUnderscoreZipCodeDto>();
        });
        configuration.AssertConfigurationIsValid();

        SourceMembers<PascalOrder, DoubleUnderscoreZipCodeDto>(configuration, nameof(DoubleUnderscoreZipCodeDto.customer__home__address__city__zip__code)).ShouldBe("Customer.HomeAddress.City.ZipCode");
        configuration.CreateMapper().Map<DoubleUnderscoreZipCodeDto>(CreatePascalOrder()).customer__home__address__city__zip__code.ShouldBe("home zip code");
    }
    [Fact]
    public void A_custom_convention_and_the_lower_underscore_one()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.SourceMemberNamingConvention = new DoubleUnderscore();
            cfg.DestinationMemberNamingConvention = LowerUnderscoreNamingConvention.Instance;
            cfg.CreateMap<DoubleUnderscoreOrder, SnakeZipCodeDto>();
            cfg.CreateProfile("TheOtherWayRound", profile =>
            {
                profile.SourceMemberNamingConvention = LowerUnderscoreNamingConvention.Instance;
                profile.DestinationMemberNamingConvention = new DoubleUnderscore();
                profile.CreateMap<SnakeOrder, DoubleUnderscoreZipCodeDto>();
            });
        });
        configuration.AssertConfigurationIsValid();
        var mapper = configuration.CreateMapper();

        SourceMembers<DoubleUnderscoreOrder, SnakeZipCodeDto>(configuration, nameof(SnakeZipCodeDto.customer_home_address_city_zip_code)).ShouldBe("customer.home__address.city.zip__code");
        mapper.Map<SnakeZipCodeDto>(CreateDoubleUnderscoreOrder()).customer_home_address_city_zip_code.ShouldBe("zip code");
        SourceMembers<SnakeOrder, DoubleUnderscoreZipCodeDto>(configuration, nameof(DoubleUnderscoreZipCodeDto.customer__home__address__city__zip__code)).ShouldBe("customer.home_address.city.zip_code");
        mapper.Map<DoubleUnderscoreZipCodeDto>(CreateSnakeOrder()).customer__home__address__city__zip__code.ShouldBe("home zip code");
    }

    [Fact]
    public void The_default_conventions_flatten_as_before()
    {
        var configuration = new MapperConfiguration(cfg => cfg.CreateMap<PascalOrder, PascalOrderDto>().ReverseMap());
        configuration.AssertConfigurationIsValid();
        var mapper = configuration.CreateMapper();

        SourceMembers<PascalOrder, PascalOrderDto>(configuration, nameof(PascalOrderDto.CustomerHomeAddressCityZipCode)).ShouldBe("Customer.HomeAddress.City.ZipCode");
        var dto = mapper.Map<PascalOrderDto>(CreatePascalOrder());
        ShouldBeFlattened(dto);
        ShouldBeUnflattened(mapper.Map<PascalOrder>(dto));
    }
    [Fact]
    public void The_same_convention_on_both_sides_flattens_as_before()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.SourceMemberNamingConvention = LowerUnderscoreNamingConvention.Instance;
            cfg.DestinationMemberNamingConvention = LowerUnderscoreNamingConvention.Instance;
            cfg.CreateMap<SnakeOrder, SnakeOrderDto>().ReverseMap();
        });
        configuration.AssertConfigurationIsValid();
        var mapper = configuration.CreateMapper();

        SourceMembers<SnakeOrder, SnakeOrderDto>(configuration, nameof(SnakeOrderDto.customer_home_address_city_zip_code)).ShouldBe("customer.home_address.city.zip_code");
        var dto = mapper.Map<SnakeOrderDto>(CreateSnakeOrder());
        ShouldBeFlattened(dto);
        ShouldBeUnflattened(mapper.Map<SnakeOrder>(dto));
    }
}
