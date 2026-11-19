namespace ForkForward.Mapper.UnitTests.ConditionClasses;

// Conditions and preconditions written as classes: IMemberCondition and IMemberPreCondition, configured with
// Condition<TCondition>(), PreCondition<TPreCondition>(), Condition(Type) and PreCondition(Type), and created
// through the service constructor like a class-based value resolver.

public class When_a_member_condition_is_a_class : AutoMapperSpecBase
{
    class Source
    {
        public int Value { get; set; }
    }
    class Destination
    {
        public int Value { get; set; }
    }
    class Positive : IMemberCondition<Source, Destination, int>
    {
        public bool ShouldMap(Source source, Destination destination, int sourceMember, int destinationMember, ResolutionContext context) => sourceMember > 0;
    }
    protected override MapperConfiguration CreateConfiguration() => new(cfg =>
        cfg.CreateMap<Source, Destination>().ForMember(d => d.Value, o => o.Condition<Positive>()));
    [Fact]
    public void Should_map_the_member_when_the_condition_is_true() =>
        Mapper.Map(new Source { Value = 5 }, new Destination { Value = 7 }).Value.ShouldBe(5);
    [Fact]
    public void Should_leave_the_member_alone_when_the_condition_is_false() =>
        Mapper.Map(new Source { Value = -5 }, new Destination { Value = 7 }).Value.ShouldBe(7);
}

public class When_a_condition_class_is_evaluated : AutoMapperSpecBase
{
    readonly Source _source = new() { Value = 3 };
    readonly Destination _destination = new() { Value = 7 };
    readonly Recorder _recorder = new();
    class Source
    {
        public int Value { get; set; }
    }
    class Destination
    {
        public int Value { get; set; }
    }
    class Recorder : IMemberCondition<Source, Destination, int>
    {
        public (Source Source, Destination Destination, int SourceMember, int DestinationMember, string State) Seen;
        public bool ShouldMap(Source source, Destination destination, int sourceMember, int destinationMember, ResolutionContext context)
        {
            Seen = (source, destination, sourceMember, destinationMember, (string)context.State);
            return true;
        }
    }
    protected override MapperConfiguration CreateConfiguration() => new(cfg =>
    {
        cfg.ConstructServicesUsing(type => _recorder);
        cfg.CreateMap<Source, Destination>().ForMember(d => d.Value, o => o.Condition<Recorder>());
    });
    [Fact]
    public void Should_get_the_source_the_destination_both_member_values_and_the_context()
    {
        Mapper.Map(_source, _destination, o => o.State = "state");
        _recorder.Seen.ShouldBe((_source, _destination, 3, 7, "state"));
    }
}

public class When_a_member_precondition_is_a_class : AutoMapperSpecBase
{
    readonly Recorder _recorder = new();
    class Source
    {
        int _value;
        public bool HasValue { get; set; }
        public bool ValueWasRead { get; private set; }
        public int Value
        {
            get
            {
                ValueWasRead = true;
                return _value;
            }
            set => _value = value;
        }
    }
    class Destination
    {
        public int Value { get; set; }
    }
    class Recorder : IMemberPreCondition<Source, Destination>
    {
        public (Source Source, Destination Destination, string State) Seen;
        public bool ShouldMap(Source source, Destination destination, ResolutionContext context)
        {
            Seen = (source, destination, (string)context.State);
            return source.HasValue;
        }
    }
    protected override MapperConfiguration CreateConfiguration() => new(cfg =>
    {
        cfg.ConstructServicesUsing(type => _recorder);
        cfg.CreateMap<Source, Destination>().ForMember(d => d.Value, o => o.PreCondition<Recorder>());
    });
    [Fact]
    public void Should_map_the_member_when_the_precondition_is_true() =>
        Mapper.Map(new Source { HasValue = true, Value = 5 }, new Destination { Value = 7 }).Value.ShouldBe(5);
    [Fact]
    public void Should_not_read_the_source_member_when_the_precondition_is_false()
    {
        var source = new Source { Value = 5 };
        Mapper.Map(source, new Destination { Value = 7 }).Value.ShouldBe(7);
        source.ValueWasRead.ShouldBeFalse();
    }
    [Fact]
    public void Should_get_the_source_the_destination_and_the_context()
    {
        var source = new Source();
        var destination = new Destination();
        Mapper.Map(source, destination, o => o.State = "state");
        _recorder.Seen.ShouldBe((source, destination, "state"));
    }
}

public class When_a_member_has_a_precondition_class_and_a_condition_class : AutoMapperSpecBase
{
    readonly List<Type> _created = [];
    class Source
    {
        public bool Enabled { get; set; }
        public int Value { get; set; }
    }
    class Destination
    {
        public int Value { get; set; }
    }
    class Enabled : IMemberPreCondition<Source, Destination>
    {
        public bool ShouldMap(Source source, Destination destination, ResolutionContext context) => source.Enabled;
    }
    class Positive : IMemberCondition<Source, Destination, int>
    {
        public bool ShouldMap(Source source, Destination destination, int sourceMember, int destinationMember, ResolutionContext context) => sourceMember > 0;
    }
    protected override MapperConfiguration CreateConfiguration() => new(cfg =>
    {
        cfg.ConstructServicesUsing(type =>
        {
            _created.Add(type);
            return Activator.CreateInstance(type);
        });
        cfg.CreateMap<Source, Destination>().ForMember(d => d.Value, o =>
        {
            o.Condition<Positive>();
            o.PreCondition<Enabled>();
        });
    });
    [Fact]
    public void Should_map_the_member_when_both_are_true()
    {
        Mapper.Map(new Source { Enabled = true, Value = 5 }, new Destination { Value = 7 }).Value.ShouldBe(5);
        _created.ShouldBe([typeof(Enabled), typeof(Positive)]);
    }
    [Fact]
    public void Should_leave_the_member_alone_when_the_condition_is_false() =>
        Mapper.Map(new Source { Enabled = true, Value = -5 }, new Destination { Value = 7 }).Value.ShouldBe(7);
    [Fact]
    public void Should_not_evaluate_the_condition_when_the_precondition_is_false()
    {
        Mapper.Map(new Source { Value = 5 }, new Destination { Value = 7 }).Value.ShouldBe(7);
        _created.ShouldBe([typeof(Enabled)]);
    }
}

public class When_one_condition_class_is_used_for_all_members : AutoMapperSpecBase
{
    class Source
    {
        public string Name { get; set; }
        public int? Age { get; set; }
        public DateTime? Born { get; set; }
        public decimal Amount { get; set; }
    }
    class Destination
    {
        public string Name { get; set; }
        public int Age { get; set; }
        public DateTime Born { get; set; }
        public decimal Amount { get; set; }
    }
    class SkipNulls : IMemberCondition<object, object, object>
    {
        public bool ShouldMap(object source, object destination, object sourceMember, object destinationMember, ResolutionContext context) => sourceMember != null;
    }
    protected override MapperConfiguration CreateConfiguration() => new(cfg =>
        cfg.CreateMap<Source, Destination>().ForAllMembers(o => o.Condition<SkipNulls>()));
    [Fact]
    public void Should_leave_alone_the_members_whose_source_value_is_null_value_types_included()
    {
        var destination = new Destination { Name = "Ann", Age = 30, Born = new DateTime(1990, 1, 1), Amount = 1 };
        Mapper.Map(new Source { Amount = 2 }, destination);
        destination.Name.ShouldBe("Ann");
        destination.Age.ShouldBe(30);
        destination.Born.ShouldBe(new DateTime(1990, 1, 1));
        destination.Amount.ShouldBe(2);
    }
    [Fact]
    public void Should_map_the_members_whose_source_value_is_not_null()
    {
        var destination = new Destination { Name = "Ann", Age = 30, Born = new DateTime(1990, 1, 1) };
        Mapper.Map(new Source { Name = "Bob", Age = 40, Born = new DateTime(1980, 1, 1) }, destination);
        destination.Name.ShouldBe("Bob");
        destination.Age.ShouldBe(40);
        destination.Born.ShouldBe(new DateTime(1980, 1, 1));
    }
}

public class When_condition_classes_are_given_as_types : AutoMapperSpecBase
{
    class Source
    {
        public bool Enabled { get; set; } = true;
        public string Name { get; set; }
        public int? Age { get; set; }
        public int Count { get; set; }
    }
    class Destination
    {
        public string Name { get; set; }
        public int Age { get; set; }
        public int Count { get; set; }
    }
    class SkipNulls : IMemberCondition<object, object, object>
    {
        public bool ShouldMap(object source, object destination, object sourceMember, object destinationMember, ResolutionContext context) => sourceMember != null;
    }
    class Positive : IMemberCondition<object, object, int>
    {
        public bool ShouldMap(object source, object destination, int sourceMember, int destinationMember, ResolutionContext context) => sourceMember > 0;
    }
    class Enabled : IMemberPreCondition<Source, object>
    {
        public bool ShouldMap(Source source, object destination, ResolutionContext context) => source.Enabled;
    }
    protected override MapperConfiguration CreateConfiguration() => new(cfg =>
        cfg.CreateMap(typeof(Source), typeof(Destination))
            .ForMember(nameof(Destination.Name), o => o.Condition(typeof(SkipNulls)))
            .ForMember(nameof(Destination.Age), o => o.Condition(typeof(SkipNulls)))
            .ForMember(nameof(Destination.Count), o => o.Condition(typeof(Positive)))
            .ForAllMembers(o => o.PreCondition(typeof(Enabled))));
    [Fact]
    public void A_condition_written_against_object_works_for_members_of_any_type()
    {
        var destination = new Destination { Name = "Ann", Age = 30 };
        Mapper.Map(new Source(), destination);
        destination.Name.ShouldBe("Ann");
        destination.Age.ShouldBe(30);
        Mapper.Map(new Source { Name = "Bob", Age = 40 }, destination);
        destination.Name.ShouldBe("Bob");
        destination.Age.ShouldBe(40);
    }
    [Fact]
    public void A_condition_written_against_the_member_type_gets_the_value_unboxed()
    {
        var destination = new Destination { Count = 7 };
        Mapper.Map(new Source { Count = -5 }, destination);
        destination.Count.ShouldBe(7);
        Mapper.Map(new Source { Count = 5 }, destination);
        destination.Count.ShouldBe(5);
    }
    [Fact]
    public void A_precondition_given_as_a_type_applies_together_with_the_condition()
    {
        var destination = new Destination { Name = "Ann", Age = 30, Count = 7 };
        Mapper.Map(new Source { Enabled = false, Name = "Bob", Age = 40, Count = 5 }, destination);
        destination.Name.ShouldBe("Ann");
        destination.Age.ShouldBe(30);
        destination.Count.ShouldBe(7);
    }
}

public class When_condition_classes_are_given_for_all_property_maps : AutoMapperSpecBase
{
    class Source
    {
        public string Name { get; set; }
        public int? Age { get; set; }
    }
    class Destination
    {
        public string Name { get; set; }
        public int Age { get; set; }
    }
    class OtherSource
    {
        public string Title { get; set; }
    }
    class OtherDestination
    {
        public string Title { get; set; }
    }
    class SkipNulls : IMemberCondition<object, object, object>
    {
        public bool ShouldMap(object source, object destination, object sourceMember, object destinationMember, ResolutionContext context) => sourceMember != null;
    }
    class NotOther : IMemberPreCondition<object, object>
    {
        public bool ShouldMap(object source, object destination, ResolutionContext context) => source is not OtherSource { Title: "skip" };
    }
    protected override MapperConfiguration CreateConfiguration() => new(cfg =>
    {
        cfg.ForAllPropertyMaps(propertyMap => true, (propertyMap, o) =>
        {
            o.Condition(typeof(SkipNulls));
            o.PreCondition<NotOther>();
        });
        cfg.CreateMap<Source, Destination>();
        cfg.CreateMap<OtherSource, OtherDestination>();
    });
    [Fact]
    public void The_same_classes_are_used_for_every_member_of_every_map()
    {
        var destination = new Destination { Name = "Ann", Age = 30 };
        Mapper.Map(new Source(), destination);
        destination.Name.ShouldBe("Ann");
        destination.Age.ShouldBe(30);
        Mapper.Map(new Source { Name = "Bob", Age = 40 }, destination);
        destination.Name.ShouldBe("Bob");
        destination.Age.ShouldBe(40);
        var otherDestination = new OtherDestination { Title = "old" };
        Mapper.Map(new OtherSource(), otherDestination);
        otherDestination.Title.ShouldBe("old");
        Mapper.Map(new OtherSource { Title = "skip" }, otherDestination);
        otherDestination.Title.ShouldBe("old");
        Mapper.Map(new OtherSource { Title = "new" }, otherDestination);
        otherDestination.Title.ShouldBe("new");
    }
}

public class When_a_type_given_as_a_condition_does_not_implement_the_interface
{
    class Source
    {
        public int Value { get; set; }
    }
    class Destination
    {
        public int Value { get; set; }
    }
    class Source<T>
    {
        public T Value { get; set; }
    }
    class Destination<T>
    {
        public T Value { get; set; }
    }
    class NotACondition
    {
    }
    class APreCondition : IMemberPreCondition<object, object>
    {
        public bool ShouldMap(object source, object destination, ResolutionContext context) => true;
    }
    [Fact]
    public void A_condition_fails_when_the_configuration_is_built()
    {
        var exception = new Action(() => new MapperConfiguration(cfg =>
            cfg.CreateMap(typeof(Source), typeof(Destination)).ForAllMembers(o => o.Condition(typeof(NotACondition))))).ShouldThrow<ArgumentException>();
        exception.ParamName.ShouldBe("conditionType");
        exception.Message.ShouldStartWith($"{typeof(NotACondition)} does not implement IMemberCondition<TSource, TDestination, TMember>.");
    }
    [Fact]
    public void A_precondition_fails_when_the_configuration_is_built()
    {
        var exception = new Action(() => new MapperConfiguration(cfg =>
            cfg.CreateMap(typeof(Source), typeof(Destination)).ForMember(nameof(Destination.Value), o => o.PreCondition(typeof(NotACondition))))).ShouldThrow<ArgumentException>();
        exception.ParamName.ShouldBe("preConditionType");
        exception.Message.ShouldStartWith($"{typeof(NotACondition)} does not implement IMemberPreCondition<TSource, TDestination>.");
    }
    [Fact]
    public void A_precondition_is_not_a_condition() =>
        new Action(() => new MapperConfiguration(cfg =>
            cfg.CreateMap(typeof(Source), typeof(Destination)).ForMember(nameof(Destination.Value), o => o.Condition(typeof(APreCondition))))).ShouldThrow<ArgumentException>();
    [Fact]
    public void It_fails_for_all_property_maps_too() =>
        new Action(() => new MapperConfiguration(cfg =>
        {
            cfg.ForAllPropertyMaps(propertyMap => true, (propertyMap, o) => o.Condition(typeof(NotACondition)));
            cfg.CreateMap<Source, Destination>();
        })).ShouldThrow<ArgumentException>();
    [Fact]
    public void An_open_generic_map_fails_when_the_configuration_is_built_and_not_when_it_first_maps() =>
        new Action(() => new MapperConfiguration(cfg =>
            cfg.CreateMap(typeof(Source<>), typeof(Destination<>)).ForMember("Value", o => o.Condition(typeof(NotACondition))))).ShouldThrow<ArgumentException>();
    [Fact]
    public void A_null_type_is_refused()
    {
        new Action(() => new MapperConfiguration(cfg =>
            cfg.CreateMap(typeof(Source), typeof(Destination)).ForMember(nameof(Destination.Value), o => o.Condition((Type)null)))).ShouldThrow<ArgumentNullException>()
            .ParamName.ShouldBe("conditionType");
        new Action(() => new MapperConfiguration(cfg =>
            cfg.CreateMap(typeof(Source), typeof(Destination)).ForMember(nameof(Destination.Value), o => o.PreCondition((Type)null)))).ShouldThrow<ArgumentNullException>()
            .ParamName.ShouldBe("preConditionType");
    }
}

public class When_a_condition_class_implements_the_interface_more_than_once
{
    class Source
    {
        public int Value { get; set; }
    }
    class Destination
    {
        public int Value { get; set; }
    }
    class Twice : IMemberCondition<Source, Destination, int>, IMemberCondition<Source, Destination, string>
    {
        public bool ShouldMap(Source source, Destination destination, int sourceMember, int destinationMember, ResolutionContext context) => true;
        public bool ShouldMap(Source source, Destination destination, string sourceMember, string destinationMember, ResolutionContext context) => true;
    }
    class TwiceBefore : IMemberPreCondition<Source, Destination>, IMemberPreCondition<Source, object>
    {
        public bool ShouldMap(Source source, Destination destination, ResolutionContext context) => true;
        public bool ShouldMap(Source source, object destination, ResolutionContext context) => true;
    }
    [Fact]
    public void A_condition_is_refused_when_the_configuration_is_built()
    {
        var exception = new Action(() => new MapperConfiguration(cfg =>
            cfg.CreateMap<Source, Destination>().ForMember(d => d.Value, o => o.Condition<Twice>()))).ShouldThrow<ArgumentException>();
        exception.ParamName.ShouldBe("TCondition");
        exception.Message.ShouldStartWith($"{typeof(Twice)} implements IMemberCondition<TSource, TDestination, TMember> more than once.");
    }
    [Fact]
    public void A_condition_given_as_a_type_is_refused_when_the_configuration_is_built() =>
        new Action(() => new MapperConfiguration(cfg =>
            cfg.CreateMap(typeof(Source), typeof(Destination)).ForAllMembers(o => o.Condition(typeof(Twice))))).ShouldThrow<ArgumentException>()
            .Message.ShouldStartWith($"{typeof(Twice)} implements IMemberCondition<TSource, TDestination, TMember> more than once.");
    [Fact]
    public void A_precondition_is_refused_when_the_configuration_is_built()
    {
        var exception = new Action(() => new MapperConfiguration(cfg =>
            cfg.CreateMap<Source, Destination>().ForMember(d => d.Value, o => o.PreCondition<TwiceBefore>()))).ShouldThrow<ArgumentException>();
        exception.ParamName.ShouldBe("TPreCondition");
        exception.Message.ShouldStartWith($"{typeof(TwiceBefore)} implements IMemberPreCondition<TSource, TDestination> more than once.");
    }
}

public class When_condition_classes_are_created_through_the_service_constructor
{
    class Source
    {
        public bool Allowed { get; set; } = true;
        public int Value { get; set; }
    }
    class Destination
    {
        public int Value { get; set; }
    }
    class AtMost : IMemberCondition<Source, Destination, int>
    {
        readonly int _limit;
        public AtMost() : this(10) { }
        public AtMost(int limit) => _limit = limit;
        public bool ShouldMap(Source source, Destination destination, int sourceMember, int destinationMember, ResolutionContext context) => sourceMember <= _limit;
    }
    class Allowed : IMemberPreCondition<Source, Destination>
    {
        readonly bool _always;
        public Allowed() : this(false) { }
        public Allowed(bool always) => _always = always;
        public bool ShouldMap(Source source, Destination destination, ResolutionContext context) => _always || source.Allowed;
    }
    class NeedsAService(string service) : IMemberCondition<Source, Destination, int>
    {
        public bool ShouldMap(Source source, Destination destination, int sourceMember, int destinationMember, ResolutionContext context) => service != null;
    }
    static void CreateMap(IMapperConfigurationExpression cfg) => cfg.CreateMap<Source, Destination>().ForMember(d => d.Value, o =>
    {
        o.PreCondition<Allowed>();
        o.Condition<AtMost>();
    });
    static object Lenient(Type type) => type == typeof(AtMost) ? new AtMost(100) : type == typeof(Allowed) ? new Allowed(true) : null;
    static int Map(IMapper mapper, Source source, Action<IMappingOperationOptions<Source, Destination>> options = null) =>
        (options == null ? mapper.Map(source, new Destination { Value = 7 }) : mapper.Map(source, new Destination { Value = 7 }, options)).Value;
    [Fact]
    public void Without_a_service_constructor_the_parameterless_constructor_is_used()
    {
        var mapper = new MapperConfiguration(CreateMap).CreateMapper();
        Map(mapper, new Source { Value = 5 }).ShouldBe(5);
        Map(mapper, new Source { Value = 50 }).ShouldBe(7);
        Map(mapper, new Source { Value = 5, Allowed = false }).ShouldBe(7);
    }
    [Fact]
    public void The_service_constructor_of_the_configuration_is_used()
    {
        var mapper = new MapperConfiguration(cfg =>
        {
            cfg.ConstructServicesUsing(Lenient);
            CreateMap(cfg);
        }).CreateMapper();
        Map(mapper, new Source { Value = 50 }).ShouldBe(50);
        Map(mapper, new Source { Value = 50, Allowed = false }).ShouldBe(50);
    }
    [Fact]
    public void The_service_constructor_of_the_mapper_is_used()
    {
        var mapper = new Mapper(new MapperConfiguration(CreateMap), Lenient);
        Map(mapper, new Source { Value = 50 }).ShouldBe(50);
        Map(mapper, new Source { Value = 50, Allowed = false }).ShouldBe(50);
    }
    [Fact]
    public void The_service_constructor_of_a_map_call_is_used_for_that_call()
    {
        var mapper = new MapperConfiguration(CreateMap).CreateMapper();
        Map(mapper, new Source { Value = 50, Allowed = false }, o => o.ConstructServicesUsing(Lenient)).ShouldBe(50);
        Map(mapper, new Source { Value = 50 }, o => o.ConstructServicesUsing(type => type == typeof(AtMost) ? new AtMost(100) : null)).ShouldBe(50);
        Map(mapper, new Source { Value = 50 }).ShouldBe(7);
    }
    [Fact]
    public void An_instance_is_created_each_time_the_condition_is_evaluated()
    {
        List<Type> created = [];
        var mapper = new MapperConfiguration(cfg =>
        {
            cfg.ConstructServicesUsing(type =>
            {
                created.Add(type);
                return Activator.CreateInstance(type);
            });
            CreateMap(cfg);
        }).CreateMapper();
        Map(mapper, new Source { Value = 5 });
        Map(mapper, new Source { Value = 5 });
        created.ShouldBe([typeof(Allowed), typeof(AtMost), typeof(Allowed), typeof(AtMost)]);
    }
    [Fact]
    public void A_class_without_a_parameterless_constructor_needs_a_service_constructor()
    {
        var configuration = new MapperConfiguration(cfg => cfg.CreateMap<Source, Destination>().ForMember(d => d.Value, o => o.Condition<NeedsAService>()));
        new Action(() => configuration.CreateMapper().Map<Destination>(new Source())).ShouldThrow<Exception>().GetBaseException().ShouldBeOfType<MissingMethodException>();
        new Mapper(configuration, type => new NeedsAService("service")).Map<Destination>(new Source { Value = 5 }).Value.ShouldBe(5);
    }
}

public class When_a_member_gets_a_condition_class_and_a_condition_lambda
{
    class Source
    {
        public int Value { get; set; }
    }
    class Destination
    {
        public int Value { get; set; }
    }
    class Never : IMemberCondition<Source, Destination, int>
    {
        public bool ShouldMap(Source source, Destination destination, int sourceMember, int destinationMember, ResolutionContext context) => false;
    }
    class Always : IMemberCondition<Source, Destination, int>
    {
        public bool ShouldMap(Source source, Destination destination, int sourceMember, int destinationMember, ResolutionContext context) => true;
    }
    class NeverBefore : IMemberPreCondition<Source, Destination>
    {
        public bool ShouldMap(Source source, Destination destination, ResolutionContext context) => false;
    }
    class AlwaysBefore : IMemberPreCondition<Source, Destination>
    {
        public bool ShouldMap(Source source, Destination destination, ResolutionContext context) => true;
    }
    static int Map(Action<IMemberConfigurationExpression<Source, Destination, int>> memberOptions) =>
        new MapperConfiguration(cfg => cfg.CreateMap<Source, Destination>().ForMember(d => d.Value, memberOptions)).CreateMapper()
            .Map(new Source { Value = 5 }, new Destination { Value = 7 }).Value;
    // Two Condition calls on a member have always meant that the last one wins; a class is no different.
    [Fact]
    public void Two_lambdas_the_last_one_wins()
    {
        Map(o => { o.Condition(source => false); o.Condition(source => true); }).ShouldBe(5);
        Map(o => { o.Condition(source => true); o.Condition(source => false); }).ShouldBe(7);
    }
    [Fact]
    public void A_class_replaces_the_lambda_configured_before_it()
    {
        Map(o => { o.Condition(source => false); o.Condition<Always>(); }).ShouldBe(5);
        Map(o => { o.Condition(source => true); o.Condition<Never>(); }).ShouldBe(7);
    }
    [Fact]
    public void A_lambda_replaces_the_class_configured_before_it()
    {
        Map(o => { o.Condition<Never>(); o.Condition(source => true); }).ShouldBe(5);
        Map(o => { o.Condition<Always>(); o.Condition(source => false); }).ShouldBe(7);
    }
    [Fact]
    public void A_class_replaces_the_class_configured_before_it()
    {
        Map(o => { o.Condition<Never>(); o.Condition<Always>(); }).ShouldBe(5);
        Map(o => { o.Condition<Always>(); o.Condition<Never>(); }).ShouldBe(7);
    }
    [Fact]
    public void A_precondition_class_replaces_the_lambda_configured_before_it_and_the_other_way_round()
    {
        Map(o => { o.PreCondition((Source source) => false); o.PreCondition<AlwaysBefore>(); }).ShouldBe(5);
        Map(o => { o.PreCondition((Source source) => true); o.PreCondition<NeverBefore>(); }).ShouldBe(7);
        Map(o => { o.PreCondition<NeverBefore>(); o.PreCondition((Source source) => true); }).ShouldBe(5);
        Map(o => { o.PreCondition<AlwaysBefore>(); o.PreCondition((Source source) => false); }).ShouldBe(7);
    }
    [Fact]
    public void A_later_ForMember_call_replaces_it_too() =>
        new MapperConfiguration(cfg => cfg.CreateMap<Source, Destination>()
            .ForMember(d => d.Value, o => o.Condition<Never>())
            .ForMember(d => d.Value, o => o.Condition(source => true))).CreateMapper()
            .Map(new Source { Value = 5 }, new Destination { Value = 7 }).Value.ShouldBe(5);
}

public class When_a_base_map_has_a_condition_class_and_a_precondition_class : AutoMapperSpecBase
{
    class BaseSource
    {
        public bool Enabled { get; set; } = true;
        public int Value { get; set; }
    }
    class DerivedSource : BaseSource
    {
    }
    class OtherSource : BaseSource
    {
    }
    class BaseDestination
    {
        public int Value { get; set; }
    }
    class DerivedDestination : BaseDestination
    {
    }
    class OtherDestination : BaseDestination
    {
    }
    class Positive : IMemberCondition<BaseSource, BaseDestination, int>
    {
        public bool ShouldMap(BaseSource source, BaseDestination destination, int sourceMember, int destinationMember, ResolutionContext context) => sourceMember > 0;
    }
    class Enabled : IMemberPreCondition<BaseSource, BaseDestination>
    {
        public bool ShouldMap(BaseSource source, BaseDestination destination, ResolutionContext context) => source.Enabled;
    }
    protected override MapperConfiguration CreateConfiguration() => new(cfg =>
    {
        cfg.CreateMap<BaseSource, BaseDestination>()
            .ForMember(d => d.Value, o =>
            {
                o.PreCondition<Enabled>();
                o.Condition<Positive>();
            })
            .Include<DerivedSource, DerivedDestination>();
        cfg.CreateMap<DerivedSource, DerivedDestination>();
        cfg.CreateMap<OtherSource, OtherDestination>()
            .IncludeBase<BaseSource, BaseDestination>()
            .ForMember(d => d.Value, o => o.Condition(source => true));
    });
    [Fact]
    public void The_derived_map_inherits_the_condition()
    {
        Mapper.Map(new DerivedSource { Value = 5 }, new DerivedDestination { Value = 7 }).Value.ShouldBe(5);
        Mapper.Map(new DerivedSource { Value = -5 }, new DerivedDestination { Value = 7 }).Value.ShouldBe(7);
    }
    [Fact]
    public void The_derived_map_inherits_the_precondition() =>
        Mapper.Map(new DerivedSource { Enabled = false, Value = 5 }, new DerivedDestination { Value = 7 }).Value.ShouldBe(7);
    [Fact]
    public void A_derived_map_replaces_what_it_configures_itself_and_inherits_the_rest()
    {
        Mapper.Map(new OtherSource { Value = -5 }, new OtherDestination { Value = 7 }).Value.ShouldBe(-5);
        Mapper.Map(new OtherSource { Enabled = false, Value = -5 }, new OtherDestination { Value = 7 }).Value.ShouldBe(7);
    }
}

public class When_an_included_member_map_has_a_condition_class : AutoMapperSpecBase
{
    class Source
    {
        public Inner Inner { get; set; }
    }
    class Inner
    {
        public string Description { get; set; }
    }
    class Destination
    {
        public string Description { get; set; }
    }
    class NotEmpty : IMemberCondition<Inner, Destination, string>
    {
        public bool ShouldMap(Inner source, Destination destination, string sourceMember, string destinationMember, ResolutionContext context) =>
            source.Description == sourceMember && sourceMember != "";
    }
    protected override MapperConfiguration CreateConfiguration() => new(cfg =>
    {
        cfg.CreateMap<Source, Destination>().IncludeMembers(s => s.Inner);
        cfg.CreateMap<Inner, Destination>(MemberList.None).ForMember(d => d.Description, o => o.Condition<NotEmpty>());
    });
    [Fact]
    public void The_condition_gets_the_included_member_as_its_source()
    {
        Mapper.Map(new Source { Inner = new() { Description = "new" } }, new Destination { Description = "old" }).Description.ShouldBe("new");
        Mapper.Map(new Source { Inner = new() { Description = "" } }, new Destination { Description = "old" }).Description.ShouldBe("old");
    }
}

public class When_an_open_generic_map_has_open_generic_condition_classes : AutoMapperSpecBase
{
    class Source<T>
    {
        public bool Enabled { get; set; } = true;
        public T Value { get; set; }
    }
    class Destination<T>
    {
        public T Value { get; set; }
    }
    // The type parameters are closed like those of an open generic value resolver:
    // with the type arguments of the source type, then those of the destination type.
    class NotDefault<T> : IMemberCondition<Source<T>, object, T>
    {
        public bool ShouldMap(Source<T> source, object destination, T sourceMember, T destinationMember, ResolutionContext context) =>
            !EqualityComparer<T>.Default.Equals(sourceMember, default);
    }
    class Enabled<TSource, TDestination> : IMemberPreCondition<Source<TSource>, Destination<TDestination>>
    {
        public bool ShouldMap(Source<TSource> source, Destination<TDestination> destination, ResolutionContext context) => source.Enabled;
    }
    protected override MapperConfiguration CreateConfiguration() => new(cfg =>
        cfg.CreateMap(typeof(Source<>), typeof(Destination<>)).ForMember("Value", o =>
        {
            o.PreCondition(typeof(Enabled<,>));
            o.Condition(typeof(NotDefault<>));
        }));
    [Fact]
    public void The_condition_is_closed_with_the_type_arguments_of_the_map()
    {
        Mapper.Map(new Source<int> { Value = 5 }, new Destination<int> { Value = 7 }).Value.ShouldBe(5);
        Mapper.Map(new Source<int>(), new Destination<int> { Value = 7 }).Value.ShouldBe(7);
        Mapper.Map(new Source<string> { Value = "new" }, new Destination<string> { Value = "old" }).Value.ShouldBe("new");
        Mapper.Map(new Source<string>(), new Destination<string> { Value = "old" }).Value.ShouldBe("old");
    }
    [Fact]
    public void The_precondition_is_closed_with_the_type_arguments_of_the_map() =>
        Mapper.Map(new Source<int> { Enabled = false, Value = 5 }, new Destination<long> { Value = 7 }).Value.ShouldBe(7);
}

public class When_a_map_inherits_from_an_open_generic_map_with_a_condition_class
{
    class Source<T>
    {
        public T Value { get; set; }
    }
    class Destination<T>
    {
        public T Value { get; set; }
    }
    class DerivedSource : Source<int>;
    class DerivedDestination : Destination<int>;
    class Positive : IMemberCondition<object, object, int>
    {
        public bool ShouldMap(object source, object destination, int sourceMember, int destinationMember, ResolutionContext context) => sourceMember > 0;
    }
    class NotDefault<T> : IMemberCondition<Source<T>, object, T>
    {
        public bool ShouldMap(Source<T> source, object destination, T sourceMember, T destinationMember, ResolutionContext context) =>
            !EqualityComparer<T>.Default.Equals(sourceMember, default);
    }
    static IMapper CreateMapper(Type conditionType, Type derivedConditionType = null) => new MapperConfiguration(cfg =>
    {
        cfg.CreateMap(typeof(Source<>), typeof(Destination<>)).ForMember("Value", o => o.Condition(conditionType));
        var derivedMap = cfg.CreateMap(typeof(DerivedSource), typeof(DerivedDestination)).IncludeBase(typeof(Source<>), typeof(Destination<>));
        if (derivedConditionType != null)
        {
            derivedMap.ForMember("Value", o => o.Condition(derivedConditionType));
        }
    }).CreateMapper();
    [Fact]
    public void A_class_that_is_not_generic_is_inherited()
    {
        var mapper = CreateMapper(typeof(Positive));
        mapper.Map(new DerivedSource { Value = 5 }, new DerivedDestination { Value = 7 }).Value.ShouldBe(5);
        mapper.Map(new DerivedSource { Value = -5 }, new DerivedDestination { Value = 7 }).Value.ShouldBe(7);
    }
    // An open generic class is closed with the type arguments of the map it is configured on. The map that inherits
    // has other type arguments, or none, so it cannot inherit the class. It fails instead of mapping without the condition.
    [Fact]
    public void An_open_generic_class_is_not_inherited_and_the_inheriting_map_says_so()
    {
        var mapper = CreateMapper(typeof(NotDefault<>));
        mapper.Map(new Source<int>(), new Destination<int> { Value = 7 }).Value.ShouldBe(7);
        var exception = new Action(() => mapper.Map(new DerivedSource { Value = 5 }, new DerivedDestination { Value = 7 })).ShouldThrow<AutoMapperMappingException>();
        exception.MemberMap.DestinationName.ShouldBe("Value");
        exception.InnerException.ShouldBeOfType<AutoMapperConfigurationException>().Message.ShouldStartWith($"{typeof(NotDefault<>)} is an open generic type.");
    }
    [Fact]
    public void The_inheriting_map_can_configure_its_own_condition()
    {
        var mapper = CreateMapper(typeof(NotDefault<>), typeof(NotDefault<int>));
        mapper.Map(new DerivedSource { Value = 5 }, new DerivedDestination { Value = 7 }).Value.ShouldBe(5);
        mapper.Map(new DerivedSource(), new DerivedDestination { Value = 7 }).Value.ShouldBe(7);
    }
}

public class When_a_map_with_a_condition_class_is_reversed : AutoMapperSpecBase
{
    class Source
    {
        public int Value { get; set; }
    }
    class Destination
    {
        public int Value { get; set; }
    }
    class Never : IMemberCondition<Source, Destination, int>
    {
        public bool ShouldMap(Source source, Destination destination, int sourceMember, int destinationMember, ResolutionContext context) => false;
    }
    class NeverBefore : IMemberPreCondition<Source, Destination>
    {
        public bool ShouldMap(Source source, Destination destination, ResolutionContext context) => false;
    }
    protected override MapperConfiguration CreateConfiguration() => new(cfg =>
        cfg.CreateMap<Source, Destination>()
            .ForMember(d => d.Value, o =>
            {
                o.PreCondition<NeverBefore>();
                o.Condition<Never>();
            })
            .ReverseMap());
    // Like a lambda condition, a condition class belongs to the map it was configured on.
    [Fact]
    public void The_reverse_map_has_no_condition()
    {
        Mapper.Map(new Source { Value = 5 }, new Destination { Value = 7 }).Value.ShouldBe(7);
        Mapper.Map(new Destination { Value = 5 }, new Source { Value = 7 }).Value.ShouldBe(5);
    }
}

public class When_projecting_a_member_with_a_condition_class : AutoMapperSpecBase
{
    readonly List<Type> _created = [];
    class Source
    {
        public int Value { get; set; }
    }
    class Destination
    {
        public int Value { get; set; }
    }
    class Never : IMemberCondition<Source, Destination, int>
    {
        public bool ShouldMap(Source source, Destination destination, int sourceMember, int destinationMember, ResolutionContext context) => false;
    }
    class NeverBefore : IMemberPreCondition<Source, Destination>
    {
        public bool ShouldMap(Source source, Destination destination, ResolutionContext context) => false;
    }
    protected override MapperConfiguration CreateConfiguration() => new(cfg =>
    {
        cfg.ConstructServicesUsing(type =>
        {
            _created.Add(type);
            return Activator.CreateInstance(type);
        });
        cfg.CreateMap<Source, Destination>().ForMember(d => d.Value, o =>
        {
            o.PreCondition<NeverBefore>();
            o.Condition<Never>();
        });
    });
    // Like lambda conditions, condition classes are not part of a projection.
    [Fact]
    public void ProjectTo_ignores_the_conditions()
    {
        ProjectTo<Destination>(new[] { new Source { Value = 5 } }.AsQueryable()).Single().Value.ShouldBe(5);
        _created.ShouldBeEmpty();
    }
}

public class When_a_condition_class_throws : AutoMapperSpecBase
{
    class Source
    {
        public int Value { get; set; }
    }
    class Destination
    {
        public int Value { get; set; }
    }
    class Failing : IMemberCondition<Source, Destination, int>
    {
        public bool ShouldMap(Source source, Destination destination, int sourceMember, int destinationMember, ResolutionContext context) =>
            throw new InvalidOperationException("The condition failed.");
    }
    protected override MapperConfiguration CreateConfiguration() => new(cfg =>
        cfg.CreateMap<Source, Destination>().ForMember(d => d.Value, o => o.Condition<Failing>()));
    [Fact]
    public void The_error_reaches_the_caller() =>
        new Action(() => Mapper.Map<Destination>(new Source())).ShouldThrow<Exception>()
            .GetBaseException().ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("The condition failed.");
    // A condition class is created through the context, so the member is one that "maps through the context".
    [CompiledPlansFact(CompiledPlansFactAttribute.ErrorNotWrapped)]
    public void The_error_names_the_member() =>
        new Action(() => Mapper.Map<Destination>(new Source())).ShouldThrow<AutoMapperMappingException>().MemberMap.DestinationName.ShouldBe(nameof(Destination.Value));
}
