namespace ForkForward.Mapper.UnitTests.Bug.NamingConventions;

// PascalCaseNamingConvention.Split starts a word at every capital: FormXML is Form, X, M, L. A PascalCase destination
// member with a run of capitals was therefore looked for in a lower_underscore source as form_x_m_l, and never found.
// When that reading finds nothing, a run of capitals is now read as one word (Form, XML) and the search is made again.
// What was matched before is matched the same way: the old reading is tried first, and Split returns what it did.
public class RunOfCapitals
{
    class Snake
    {
        public string form_xml { get; set; }
        public int user_id { get; set; }
        public string a_b_count { get; set; }
    }
    class Pascal
    {
        public string FormXML { get; set; }
        public int UserId { get; set; }
        public string ABCount { get; set; }
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
    static string[] SourceMembers<TSource, TDestination>(MapperConfiguration configuration, string destinationMember) =>
        [..configuration.FindTypeMapFor<TSource, TDestination>().PropertyMaps.Single(propertyMap => propertyMap.DestinationName == destinationMember).SourceMembers.Select(member => member.Name)];

    [Fact]
    public void The_reported_map()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.SourceMemberNamingConvention = LowerUnderscoreNamingConvention.Instance;
            cfg.DestinationMemberNamingConvention = PascalCaseNamingConvention.Instance;
            cfg.CreateMap<Snake, Pascal>();
        });
        configuration.AssertConfigurationIsValid();

        var destination = configuration.CreateMapper().Map<Pascal>(new Snake { form_xml = "<form/>", user_id = 7, a_b_count = "three" });
        destination.FormXML.ShouldBe("<form/>");
        destination.UserId.ShouldBe(7);
        destination.ABCount.ShouldBe("three");
    }

    class SnakeWords
    {
        public string form_xml { get; set; }
        public string xml_file { get; set; }
        public int user_id { get; set; }
        public string https_connection { get; set; }
        public string xml_http_request { get; set; }
        public string html5_parser { get; set; }
        public string sha256_hash { get; set; }
        public string address2_line { get; set; }
        public string utf8_string { get; set; }
        public int order_id { get; set; }
        public string plain_name { get; set; }
        public string a_b_count { get; set; }
    }
    class PascalWords
    {
        public string FormXML { get; set; }
        public string XMLFile { get; set; }
        public int UserID { get; set; }
        public string HTTPSConnection { get; set; }
        public string XmlHTTPRequest { get; set; }
        public string HTML5Parser { get; set; }
        public string SHA256Hash { get; set; }
        public string Address2Line { get; set; }
        public string Utf8String { get; set; }
        public int OrderId { get; set; }
        public string PlainName { get; set; }
        public string ABCount { get; set; }
    }
    static SnakeWords CreateSnakeWords() => new()
    {
        form_xml = "form", xml_file = "file", user_id = 7, https_connection = "connection", xml_http_request = "request", html5_parser = "parser",
        sha256_hash = "hash", address2_line = "line", utf8_string = "string", order_id = 9, plain_name = "name", a_b_count = "count"
    };
    static void ShouldBeTheSameWords(PascalWords pascal, SnakeWords snake)
    {
        pascal.FormXML.ShouldBe(snake.form_xml);
        pascal.XMLFile.ShouldBe(snake.xml_file);
        pascal.UserID.ShouldBe(snake.user_id);
        pascal.HTTPSConnection.ShouldBe(snake.https_connection);
        pascal.XmlHTTPRequest.ShouldBe(snake.xml_http_request);
        pascal.HTML5Parser.ShouldBe(snake.html5_parser);
        pascal.SHA256Hash.ShouldBe(snake.sha256_hash);
        pascal.Address2Line.ShouldBe(snake.address2_line);
        pascal.Utf8String.ShouldBe(snake.utf8_string);
        pascal.OrderId.ShouldBe(snake.order_id);
        pascal.PlainName.ShouldBe(snake.plain_name);
        pascal.ABCount.ShouldBe(snake.a_b_count);
    }
    [Fact]
    public void A_run_of_capitals_is_one_word()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            LowerUnderscoreToPascalCase(cfg);
            cfg.CreateMap<SnakeWords, PascalWords>();
        });
        configuration.AssertConfigurationIsValid();

        // a run at the end of the name, at its start, of two letters, of five, and in the middle: the last capital of
        // a run starts the next word when a lower case letter follows it
        SourceMembers<SnakeWords, PascalWords>(configuration, nameof(PascalWords.FormXML)).ShouldBe([nameof(SnakeWords.form_xml)]);
        SourceMembers<SnakeWords, PascalWords>(configuration, nameof(PascalWords.XMLFile)).ShouldBe([nameof(SnakeWords.xml_file)]);
        SourceMembers<SnakeWords, PascalWords>(configuration, nameof(PascalWords.UserID)).ShouldBe([nameof(SnakeWords.user_id)]);
        SourceMembers<SnakeWords, PascalWords>(configuration, nameof(PascalWords.HTTPSConnection)).ShouldBe([nameof(SnakeWords.https_connection)]);
        SourceMembers<SnakeWords, PascalWords>(configuration, nameof(PascalWords.XmlHTTPRequest)).ShouldBe([nameof(SnakeWords.xml_http_request)]);
        var source = CreateSnakeWords();
        ShouldBeTheSameWords(configuration.CreateMapper().Map<PascalWords>(source), source);
    }
    [Fact]
    public void A_digit_stays_in_the_word_it_follows()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            LowerUnderscoreToPascalCase(cfg);
            cfg.CreateMap<SnakeWords, PascalWords>();
        });

        // a digit does not end a run of capitals and does not start a word, the capital after it does
        SourceMembers<SnakeWords, PascalWords>(configuration, nameof(PascalWords.HTML5Parser)).ShouldBe([nameof(SnakeWords.html5_parser)]);
        SourceMembers<SnakeWords, PascalWords>(configuration, nameof(PascalWords.SHA256Hash)).ShouldBe([nameof(SnakeWords.sha256_hash)]);
        // as it was
        SourceMembers<SnakeWords, PascalWords>(configuration, nameof(PascalWords.Address2Line)).ShouldBe([nameof(SnakeWords.address2_line)]);
        SourceMembers<SnakeWords, PascalWords>(configuration, nameof(PascalWords.Utf8String)).ShouldBe([nameof(SnakeWords.utf8_string)]);
    }
    [Fact]
    public void Names_that_were_matched_are_matched_as_before()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            LowerUnderscoreToPascalCase(cfg);
            cfg.CreateMap<SnakeWords, PascalWords>();
        });

        SourceMembers<SnakeWords, PascalWords>(configuration, nameof(PascalWords.ABCount)).ShouldBe([nameof(SnakeWords.a_b_count)]);
        SourceMembers<SnakeWords, PascalWords>(configuration, nameof(PascalWords.OrderId)).ShouldBe([nameof(SnakeWords.order_id)]);
        SourceMembers<SnakeWords, PascalWords>(configuration, nameof(PascalWords.PlainName)).ShouldBe([nameof(SnakeWords.plain_name)]);
    }

    class SnakeBothReadings
    {
        public string a_b_count { get; set; }
        public string ab_count { get; set; }
        public string form_x_m_l { get; set; }
        public string form_xml { get; set; }
        public SnakeInner x_m_l { get; set; }
        public SnakeInner xml { get; set; }
    }
    class SnakeRunsOnly
    {
        public string ab_count { get; set; }
        public string form_xml { get; set; }
        public SnakeInner xml { get; set; }
    }
    class SnakeInner
    {
        public string name { get; set; }
    }
    class PascalBothReadings
    {
        public string ABCount { get; set; }
        public string FormXML { get; set; }
        public string XMLName { get; set; }
    }
    [Fact]
    public void Where_both_readings_find_a_source_member_the_one_found_before_wins()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            LowerUnderscoreToPascalCase(cfg);
            cfg.CreateMap<SnakeBothReadings, PascalBothReadings>();
        });
        configuration.AssertConfigurationIsValid();

        SourceMembers<SnakeBothReadings, PascalBothReadings>(configuration, nameof(PascalBothReadings.ABCount)).ShouldBe([nameof(SnakeBothReadings.a_b_count)]);
        SourceMembers<SnakeBothReadings, PascalBothReadings>(configuration, nameof(PascalBothReadings.FormXML)).ShouldBe([nameof(SnakeBothReadings.form_x_m_l)]);
        SourceMembers<SnakeBothReadings, PascalBothReadings>(configuration, nameof(PascalBothReadings.XMLName)).ShouldBe([nameof(SnakeBothReadings.x_m_l), nameof(SnakeInner.name)]);
        var destination = configuration.CreateMapper().Map<PascalBothReadings>(new SnakeBothReadings
        {
            a_b_count = "every capital", ab_count = "run", form_x_m_l = "every capital", form_xml = "run", x_m_l = new() { name = "every capital" }, xml = new() { name = "run" }
        });
        destination.ABCount.ShouldBe("every capital");
        destination.FormXML.ShouldBe("every capital");
        destination.XMLName.ShouldBe("every capital");
    }
    [Fact]
    public void Where_only_the_run_of_capitals_finds_a_source_member_it_is_matched()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            LowerUnderscoreToPascalCase(cfg);
            cfg.CreateMap<SnakeRunsOnly, PascalBothReadings>();
        });
        configuration.AssertConfigurationIsValid();

        var destination = configuration.CreateMapper().Map<PascalBothReadings>(new SnakeRunsOnly { ab_count = "count", form_xml = "form", xml = new() { name = "name" } });
        destination.ABCount.ShouldBe("count");
        destination.FormXML.ShouldBe("form");
        destination.XMLName.ShouldBe("name");
    }

    [Fact]
    public void A_lower_underscore_destination_from_a_pascal_case_source()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            PascalCaseToLowerUnderscore(cfg);
            cfg.CreateMap<PascalWords, SnakeWords>();
        });
        configuration.AssertConfigurationIsValid();

        var source = configuration.CreateMapper().Map<SnakeWords>(new PascalWords
        {
            FormXML = "form", XMLFile = "file", UserID = 7, HTTPSConnection = "connection", XmlHTTPRequest = "request", HTML5Parser = "parser",
            SHA256Hash = "hash", Address2Line = "line", Utf8String = "string", OrderId = 9, PlainName = "name", ABCount = "count"
        });
        source.form_xml.ShouldBe("form");
        source.xml_file.ShouldBe("file");
        source.user_id.ShouldBe(7);
        source.https_connection.ShouldBe("connection");
        source.xml_http_request.ShouldBe("request");
        source.html5_parser.ShouldBe("parser");
        source.sha256_hash.ShouldBe("hash");
        source.address2_line.ShouldBe("line");
        source.utf8_string.ShouldBe("string");
        source.order_id.ShouldBe(9);
        source.plain_name.ShouldBe("name");
        source.a_b_count.ShouldBe("count");
    }
    [Fact]
    public void ReverseMap_from_lower_underscore_to_pascal_case()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            LowerUnderscoreToPascalCase(cfg);
            cfg.CreateMap<SnakeWords, PascalWords>().ReverseMap();
        });
        configuration.AssertConfigurationIsValid();
        var mapper = configuration.CreateMapper();

        var source = CreateSnakeWords();
        var destination = mapper.Map<PascalWords>(source);
        ShouldBeTheSameWords(destination, source);
        ShouldBeTheSameWords(destination, mapper.Map<SnakeWords>(destination));
    }
    [Fact]
    public void ReverseMap_from_pascal_case_to_lower_underscore()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            PascalCaseToLowerUnderscore(cfg);
            cfg.CreateMap<PascalWords, SnakeWords>().ReverseMap();
        });
        configuration.AssertConfigurationIsValid();
        var mapper = configuration.CreateMapper();

        // the reverse map is the one with the PascalCase destination here
        configuration.FindTypeMapFor<SnakeWords, PascalWords>().PropertyMaps.Count.ShouldBe(typeof(PascalWords).GetProperties().Length);
        var source = CreateSnakeWords();
        var destination = mapper.Map<PascalWords>(source);
        ShouldBeTheSameWords(destination, source);
        ShouldBeTheSameWords(destination, mapper.Map<SnakeWords>(destination));
    }

    class SnakeOrder
    {
        public SnakeCustomer customer { get; set; }
        public SnakeCustomer xml_customer { get; set; }
    }
    class SnakeCustomer
    {
        public string name { get; set; }
        public string xml_name { get; set; }
    }
    class PascalOrderDto
    {
        public string CustomerName { get; set; }
        public string CustomerXMLName { get; set; }
        public string XMLCustomerName { get; set; }
        public string XMLCustomerXMLName { get; set; }
    }
    class PascalOrder
    {
        public PascalCustomer Customer { get; set; }
        public PascalCustomer XMLCustomer { get; set; }
    }
    class PascalCustomer
    {
        public string Name { get; set; }
        public string XMLName { get; set; }
    }
    class SnakeOrderDto
    {
        public string customer_name { get; set; }
        public string customer_xml_name { get; set; }
        public string xml_customer_name { get; set; }
        public string xml_customer_xml_name { get; set; }
    }
    [Fact]
    public void Flattening_and_unflattening_with_a_run_of_capitals_in_the_path()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            LowerUnderscoreToPascalCase(cfg);
            cfg.CreateMap<SnakeOrder, PascalOrderDto>().ReverseMap();
        });
        configuration.AssertConfigurationIsValid();
        var mapper = configuration.CreateMapper();

        SourceMembers<SnakeOrder, PascalOrderDto>(configuration, nameof(PascalOrderDto.CustomerXMLName)).ShouldBe([nameof(SnakeOrder.customer), nameof(SnakeCustomer.xml_name)]);
        var dto = mapper.Map<PascalOrderDto>(new SnakeOrder { customer = new() { name = "name", xml_name = "xml name" }, xml_customer = new() { name = "other name", xml_name = "other xml name" } });
        dto.CustomerName.ShouldBe("name");
        dto.CustomerXMLName.ShouldBe("xml name");
        dto.XMLCustomerName.ShouldBe("other name");
        dto.XMLCustomerXMLName.ShouldBe("other xml name");

        var order = mapper.Map<SnakeOrder>(dto);
        order.customer.name.ShouldBe("name");
        order.customer.xml_name.ShouldBe("xml name");
        order.xml_customer.name.ShouldBe("other name");
        order.xml_customer.xml_name.ShouldBe("other xml name");
    }
    [Fact]
    public void Flattening_and_unflattening_into_lower_underscore_with_a_run_of_capitals_in_the_path()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            PascalCaseToLowerUnderscore(cfg);
            cfg.CreateMap<PascalOrder, SnakeOrderDto>().ReverseMap();
        });
        configuration.AssertConfigurationIsValid();
        var mapper = configuration.CreateMapper();

        var dto = mapper.Map<SnakeOrderDto>(new PascalOrder { Customer = new() { Name = "name", XMLName = "xml name" }, XMLCustomer = new() { Name = "other name", XMLName = "other xml name" } });
        dto.customer_name.ShouldBe("name");
        dto.customer_xml_name.ShouldBe("xml name");
        dto.xml_customer_name.ShouldBe("other name");
        dto.xml_customer_xml_name.ShouldBe("other xml name");

        var order = mapper.Map<PascalOrder>(dto);
        order.Customer.Name.ShouldBe("name");
        order.Customer.XMLName.ShouldBe("xml name");
        order.XMLCustomer.Name.ShouldBe("other name");
        order.XMLCustomer.XMLName.ShouldBe("other xml name");
    }

    class PascalWithConstructor(string formXML, int userID)
    {
        public string FormXML { get; } = formXML;
        public int UserID { get; } = userID;
    }
    [Fact]
    public void A_constructor_parameter()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            LowerUnderscoreToPascalCase(cfg);
            cfg.CreateMap<Snake, PascalWithConstructor>();
        });
        configuration.AssertConfigurationIsValid();

        var destination = configuration.CreateMapper().Map<PascalWithConstructor>(new Snake { form_xml = "<form/>", user_id = 7 });
        destination.FormXML.ShouldBe("<form/>");
        destination.UserID.ShouldBe(7);
    }

    class PascalLetters
    {
        public string FormXml { get; set; }
        public int UserId { get; set; }
        public string AbCount { get; set; }
    }
    [Fact]
    public void A_convention_of_a_profile_and_a_convention_of_the_configuration()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            LowerUnderscoreToPascalCase(cfg);
            cfg.CreateMap<Snake, Pascal>();
            cfg.CreateProfile("TheConventionsOfTheConfiguration", profile => profile.CreateMap<SnakeWords, PascalWords>());
            cfg.CreateProfile("ItsOwnConventions", profile =>
            {
                PascalCaseToLowerUnderscore(profile);
                profile.CreateMap<PascalOrder, SnakeOrderDto>().ReverseMap();
            });
            cfg.CreateProfile("TheDefaultConventions", profile =>
            {
                profile.SourceMemberNamingConvention = PascalCaseNamingConvention.Instance;
                profile.CreateMap<PascalLetters, Pascal>();
                profile.CreateMap<PascalOrder, PascalOrderDto>();
            });
        });
        configuration.AssertConfigurationIsValid();
        var mapper = configuration.CreateMapper();

        mapper.Map<Pascal>(new Snake { form_xml = "<form/>" }).FormXML.ShouldBe("<form/>");
        var source = CreateSnakeWords();
        ShouldBeTheSameWords(mapper.Map<PascalWords>(source), source);
        var order = new PascalOrder { Customer = new() { XMLName = "xml name" }, XMLCustomer = new() { Name = "other name" } };
        var dto = mapper.Map<SnakeOrderDto>(order);
        dto.customer_xml_name.ShouldBe("xml name");
        dto.xml_customer_name.ShouldBe("other name");
        mapper.Map<PascalOrder>(dto).Customer.XMLName.ShouldBe("xml name");
        mapper.Map<PascalOrder>(dto).XMLCustomer.Name.ShouldBe("other name");
        mapper.Map<Pascal>(new PascalLetters { FormXml = "<form/>", AbCount = "three" }).FormXML.ShouldBe("<form/>");
        mapper.Map<PascalOrderDto>(order).CustomerXMLName.ShouldBe("xml name");
        mapper.Map<PascalOrderDto>(order).XMLCustomerName.ShouldBe("other name");
    }
    [Fact]
    public void A_convention_of_a_profile_only()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.CreateProfile("LowerUnderscoreToPascalCase", profile =>
            {
                LowerUnderscoreToPascalCase(profile);
                profile.CreateMap<Snake, Pascal>();
            });
            cfg.CreateProfile("LowerUnderscoreSourceOnly", profile =>
            {
                profile.SourceMemberNamingConvention = LowerUnderscoreNamingConvention.Instance;
                profile.CreateMap<SnakeWords, PascalWords>();
            });
        });
        configuration.AssertConfigurationIsValid();
        var mapper = configuration.CreateMapper();

        mapper.Map<Pascal>(new Snake { form_xml = "<form/>" }).FormXML.ShouldBe("<form/>");
        var source = CreateSnakeWords();
        ShouldBeTheSameWords(mapper.Map<PascalWords>(source), source);
    }

    class DoubleUnderscore : INamingConvention
    {
        public string SeparatorCharacter => "__";
        public string[] Split(string input) => input.Split("__", StringSplitOptions.RemoveEmptyEntries);
    }
    class EveryCapital : INamingConvention
    {
        public string SeparatorCharacter => "";
        public string[] Split(string input) => PascalCaseNamingConvention.Instance.Split(input);
    }
    class DoubleUnderscored
    {
        public string form__xml { get; set; }
        public int user__id { get; set; }
        public string a__b__count { get; set; }
    }
    [Fact]
    public void A_source_convention_with_another_separator()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.SourceMemberNamingConvention = new DoubleUnderscore();
            cfg.CreateMap<DoubleUnderscored, Pascal>();
        });
        configuration.AssertConfigurationIsValid();

        var destination = configuration.CreateMapper().Map<Pascal>(new DoubleUnderscored { form__xml = "<form/>", user__id = 7, a__b__count = "three" });
        destination.FormXML.ShouldBe("<form/>");
        destination.UserId.ShouldBe(7);
        destination.ABCount.ShouldBe("three");
    }
    [Fact]
    public void A_destination_convention_that_is_not_the_pascal_case_one_is_read_as_it_splits()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.SourceMemberNamingConvention = LowerUnderscoreNamingConvention.Instance;
            cfg.DestinationMemberNamingConvention = new EveryCapital();
            cfg.CreateMap<Snake, Pascal>();
        });

        Should.Throw<AutoMapperConfigurationException>(configuration.AssertConfigurationIsValid)
            .Errors.Single().UnmappedPropertyNames.ShouldBe([nameof(Pascal.FormXML)]);
    }
    [Fact]
    public void PascalCaseNamingConvention_splits_as_before()
    {
        var convention = PascalCaseNamingConvention.Instance;

        convention.Split("FormXML").ShouldBe(["Form", "X", "M", "L"]);
        convention.Split("XMLFile").ShouldBe(["X", "M", "L", "File"]);
        convention.Split("UserID").ShouldBe(["User", "I", "D"]);
        convention.Split("UserId").ShouldBe(["User", "Id"]);
        convention.Split("ABCount").ShouldBe(["A", "B", "Count"]);
        convention.Split("HTML5Parser").ShouldBe(["H", "T", "M", "L5", "Parser"]);
        convention.Split("Address2Line").ShouldBe(["Address2", "Line"]);
        convention.Split("formXML").ShouldBe(["form", "X", "M", "L"]);
        convention.Split("Name").ShouldBeEmpty();
        convention.Split("").ShouldBeEmpty();
    }
    [Fact]
    public void The_default_conventions_do_not_split_names_into_words()
    {
        // PascalCase on both sides: a name is cut before every capital and each piece is looked up as it is, without
        // regard to case, so there is nothing to join again and a run of capitals needs no reading.
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<PascalLetters, Pascal>();
            cfg.CreateMap<PascalOrder, PascalOrderDto>();
        });
        configuration.AssertConfigurationIsValid();
        var mapper = configuration.CreateMapper();

        var destination = mapper.Map<Pascal>(new PascalLetters { FormXml = "<form/>", UserId = 7, AbCount = "three" });
        destination.FormXML.ShouldBe("<form/>");
        destination.UserId.ShouldBe(7);
        destination.ABCount.ShouldBe("three");
        var dto = mapper.Map<PascalOrderDto>(new PascalOrder { Customer = new() { Name = "name", XMLName = "xml name" }, XMLCustomer = new() { Name = "other name", XMLName = "other xml name" } });
        dto.CustomerName.ShouldBe("name");
        dto.CustomerXMLName.ShouldBe("xml name");
        dto.XMLCustomerName.ShouldBe("other name");
        dto.XMLCustomerXMLName.ShouldBe("other xml name");
    }
}
