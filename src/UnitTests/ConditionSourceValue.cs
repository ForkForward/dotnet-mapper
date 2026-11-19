namespace ForkForward.Mapper.UnitTests.ConditionSourceValue;

// What a condition is given as the source member: the third argument of a condition lambda, sourceMember of
// IMemberCondition.
//
// In AutoMapper 14 it was always the value after it had been mapped to the type of the destination member.
// A condition that takes the source member as object could not tell a null from a default value: a null int?
// that goes into an int arrived as 0. "Skip nulls for all members" never skipped such a member, and the
// destination was overwritten with the default value.
//
// Now: when the condition declares the source member with a type that is not the type of the destination member,
// and the resolved source value can be passed as that type without a conversion (the same type, a reference
// conversion, or boxing), the condition gets the resolved source value. That is ForAllMembers, ForMember by name,
// the non-generic API, ForAllPropertyMaps, and condition classes written against object or a base type.
// In every other case the condition gets what it always got; ForMember(d => d.Count, o => o.Condition(...))
// declares the type of the destination member and is not affected.
//
// The first classes show the change: each fact says what the condition saw before and what it sees now.
// The classes after them pin what did not change.

public class When_a_condition_for_all_members_skips_nulls : AutoMapperSpecBase
{
    class Source
    {
        public int? Count { get; set; }
        public bool? Flag { get; set; }
        public DateTime? Date { get; set; }
        public string Text { get; set; }
    }
    class Destination
    {
        public int Count { get; set; }
        public bool Flag { get; set; }
        public DateTime Date { get; set; }
        public int Text { get; set; }
    }
    protected override MapperConfiguration CreateConfiguration() => new(cfg =>
        cfg.CreateMap<Source, Destination>().ForAllMembers(o => o.Condition((source, destination, sourceMember) => sourceMember != null)));
    [Fact]
    public void A_null_that_goes_into_an_int_is_skipped_and_no_longer_overwrites_it_with_zero()
    {
        // Before: the condition saw 0, the null mapped to int. It did not skip, and Count became 0.
        // Now: the condition sees null, and Count keeps its value.
        var destination = new Destination { Count = 7 };
        Mapper.Map(new Source { Text = "12" }, destination);
        destination.Count.ShouldBe(7);
        destination.Text.ShouldBe(12);
    }
    [Fact]
    public void A_null_that_goes_into_a_bool_is_skipped_and_no_longer_overwrites_it_with_false()
    {
        // Before: the condition saw false, and Flag became false. Now: it sees null, and Flag stays true.
        var destination = new Destination { Flag = true };
        Mapper.Map(new Source(), destination);
        destination.Flag.ShouldBeTrue();
    }
    [Fact]
    public void A_null_that_goes_into_a_date_is_skipped_and_no_longer_overwrites_it_with_the_default_date()
    {
        // Before: the condition saw 0001-01-01, and Date became that. Now: it sees null, and Date keeps its value.
        var destination = new Destination { Date = new DateTime(2026, 11, 19) };
        Mapper.Map(new Source(), destination);
        destination.Date.ShouldBe(new DateTime(2026, 11, 19));
    }
    [Fact]
    public void A_value_is_mapped_as_before()
    {
        var destination = new Destination { Count = 7, Flag = true, Date = new DateTime(2026, 11, 19), Text = 3 };
        Mapper.Map(new Source { Count = 0, Flag = false, Date = new DateTime(2000, 1, 1), Text = "12" }, destination);
        destination.Count.ShouldBe(0);
        destination.Flag.ShouldBeFalse();
        destination.Date.ShouldBe(new DateTime(2000, 1, 1));
        destination.Text.ShouldBe(12);
    }
}

public class When_a_condition_takes_the_source_member_as_object : AutoMapperSpecBase
{
    readonly Dictionary<string, object> _sourceMembers = [];
    readonly Dictionary<string, object> _destinationMembers = [];
    readonly List<int> _destinationItems = [1, 2];
    readonly Child _child = new() { Value = 1 };
    Destination _destination;
    class Source
    {
        public int? Count { get; set; }
        public bool? Flag { get; set; }
        public string Text { get; set; }
        public int Number { get; set; }
        public List<int> Items { get; set; }
        public string Name { get; set; }
        public Child Child { get; set; }
    }
    class Destination
    {
        public int Count { get; set; }
        public bool Flag { get; set; }
        public int Text { get; set; }
        public long Number { get; set; }
        public List<int> Items { get; set; }
        public string Name { get; set; }
        public ChildDto Child { get; set; }
    }
    class Child
    {
        public int Value { get; set; }
    }
    class ChildDto
    {
        public int Value { get; set; }
    }
    protected override MapperConfiguration CreateConfiguration() => new(cfg =>
    {
        cfg.CreateMap<Child, ChildDto>();
        cfg.CreateMap<Source, Destination>()
            .ForMember(d => d.Name, o => o.AddTransform(name => name.Trim()))
            .ForAllMembers(o => o.Condition((source, destination, sourceMember, destinationMember) =>
            {
                _sourceMembers[o.DestinationMember.Name] = sourceMember;
                _destinationMembers[o.DestinationMember.Name] = destinationMember;
                return true;
            }));
    });
    protected override void Because_of() => Mapper.Map(new Source { Text = "12", Number = 42, Name = " Ann ", Child = _child },
        _destination = new Destination { Count = 7, Flag = true, Text = 3, Number = 4, Items = _destinationItems, Name = "old" });
    [Fact]
    public void A_null_int_arrives_as_null_and_no_longer_as_zero() =>
        // Before: 0, a boxed int. Now: null.
        _sourceMembers["Count"].ShouldBeNull();
    [Fact]
    public void A_null_bool_arrives_as_null_and_no_longer_as_false() =>
        // Before: false, a boxed bool. Now: null.
        _sourceMembers["Flag"].ShouldBeNull();
    [Fact]
    public void A_string_that_goes_into_an_int_arrives_as_the_string_and_no_longer_as_the_converted_int() =>
        // Before: 12, a boxed int. Now: "12".
        _sourceMembers["Text"].ShouldBeOfType<string>().ShouldBe("12");
    [Fact]
    public void An_int_that_goes_into_a_long_arrives_as_an_int_and_no_longer_as_a_long() =>
        // Before: 42L, a boxed long. Now: 42, a boxed int.
        _sourceMembers["Number"].ShouldBeOfType<int>().ShouldBe(42);
    [Fact]
    public void An_object_that_is_mapped_to_another_type_arrives_as_the_source_object_and_no_longer_as_the_mapped_one() =>
        // Before: the ChildDto the Child was mapped to. Now: the Child.
        _sourceMembers["Child"].ShouldBeSameAs(_child);
    [Fact]
    public void A_null_collection_arrives_as_null_and_no_longer_as_the_empty_collection_it_is_mapped_to() =>
        // Before: a new empty List<int>, what a null collection is mapped to unless AllowNullCollections is set,
        // although the source and destination members have the same type. Now: null.
        _sourceMembers["Items"].ShouldBeNull();
    [Fact]
    public void A_value_arrives_as_it_was_resolved_and_no_longer_with_the_value_transformers_applied() =>
        // Before: "Ann", trimmed by the transformer of the member. Now: " Ann ", the source value.
        _sourceMembers["Name"].ShouldBe(" Ann ");
    [Fact]
    public void The_destination_member_values_arrive_as_before()
    {
        _destinationMembers["Count"].ShouldBe(7);
        _destinationMembers["Flag"].ShouldBe(true);
        _destinationMembers["Text"].ShouldBe(3);
        _destinationMembers["Number"].ShouldBe(4L);
        _destinationMembers["Items"].ShouldBeSameAs(_destinationItems);
        _destinationMembers["Name"].ShouldBe("old");
        _destinationMembers["Child"].ShouldBeNull();
    }
    [Fact]
    public void What_is_assigned_when_the_condition_says_yes_is_the_mapped_value_as_before()
    {
        _destination.Count.ShouldBe(0);
        _destination.Flag.ShouldBeFalse();
        _destination.Text.ShouldBe(12);
        _destination.Number.ShouldBe(42);
        _destination.Items.ShouldBeEmpty();
        _destination.Items.ShouldNotBeSameAs(_destinationItems);
        _destination.Name.ShouldBe("Ann");
        _destination.Child.Value.ShouldBe(1);
    }
}

public class When_a_condition_takes_the_source_member_as_object_through_the_other_apis
{
    object _sourceMember = "not evaluated";
    class Source
    {
        public int? Count { get; set; }
    }
    class Destination
    {
        public int Count { get; set; }
    }
    bool Record(object sourceMember)
    {
        _sourceMember = sourceMember;
        return sourceMember != null;
    }
    static int Map(MapperConfiguration configuration) => configuration.CreateMapper().Map(new Source(), new Destination { Count = 7 }).Count;
    // In each of these the condition saw 0 before, and Count became 0. Now it sees null, and Count stays 7.
    [Fact]
    public void ForMember_by_name()
    {
        Map(new(cfg => cfg.CreateMap<Source, Destination>().ForMember(nameof(Destination.Count), o => o.Condition((source, destination, sourceMember) => Record(sourceMember))))).ShouldBe(7);
        _sourceMember.ShouldBeNull();
    }
    [Fact]
    public void A_map_created_from_types()
    {
        Map(new(cfg => cfg.CreateMap(typeof(Source), typeof(Destination)).ForMember(nameof(Destination.Count), o => o.Condition((source, destination, sourceMember) => Record(sourceMember))))).ShouldBe(7);
        _sourceMember.ShouldBeNull();
    }
    [Fact]
    public void ForAllMembers_of_a_map_created_from_types()
    {
        Map(new(cfg => cfg.CreateMap(typeof(Source), typeof(Destination)).ForAllMembers(o => o.Condition((source, destination, sourceMember) => Record(sourceMember))))).ShouldBe(7);
        _sourceMember.ShouldBeNull();
    }
    [Fact]
    public void ForAllPropertyMaps()
    {
        Map(new(cfg =>
        {
            cfg.ForAllPropertyMaps(propertyMap => true, (propertyMap, o) => o.Condition((source, destination, sourceMember) => Record(sourceMember)));
            cfg.CreateMap<Source, Destination>();
        })).ShouldBe(7);
        _sourceMember.ShouldBeNull();
    }
    [Fact]
    public void ForAllMaps()
    {
        Map(new(cfg =>
        {
            cfg.ForAllMaps((typeMap, map) => map.ForAllMembers(o => o.Condition((source, destination, sourceMember) => Record(sourceMember))));
            cfg.CreateMap<Source, Destination>();
        })).ShouldBe(7);
        _sourceMember.ShouldBeNull();
    }
    [Fact]
    public void A_member_expression_typed_as_object()
    {
        Expression<Func<Destination, object>> count = d => d.Count;
        Map(new(cfg => cfg.CreateMap<Source, Destination>().ForMember(count, o => o.Condition((source, destination, sourceMember) => Record(sourceMember))))).ShouldBe(7);
        _sourceMember.ShouldBeNull();
    }
}

// Condition classes are new, so there is no "before" here: these show which value a class gets.
public class When_a_condition_class_declares_the_source_member
{
    class Source
    {
        public int? Count { get; set; }
        public int Number { get; set; }
        public string[] Names { get; set; }
    }
    class Destination
    {
        public int Count { get; set; }
        public long Number { get; set; }
        public List<string> Names { get; set; }
    }
    abstract class Recorder<TMember> : IMemberCondition<object, object, TMember>
    {
        public object SourceMember = "not evaluated";
        public bool ShouldMap(object source, object destination, TMember sourceMember, TMember destinationMember, ResolutionContext context)
        {
            SourceMember = sourceMember;
            return true;
        }
    }
    class AsObject : Recorder<object>;
    class AsEnumerable : Recorder<IEnumerable>;
    class AsComparable : Recorder<IComparable>;
    class AsInt : Recorder<int>;
    class AsLong : Recorder<long>;
    class AsDecimal : Recorder<decimal>;
    static (Source Source, Destination Destination) Map(object recorder, Action<IMapperConfigurationExpression> createMap)
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.ConstructServicesUsing(type => recorder);
            // What a mapped int looks like is unmistakable: ten times the source value.
            cfg.CreateMap<int, long>().ConvertUsing(source => source * 10L);
            createMap(cfg);
        });
        var source = new Source { Number = 42, Names = ["Ann"] };
        return (source, configuration.CreateMapper().Map(source, new Destination { Count = 7 }));
    }
    [Fact]
    public void Written_against_object_it_gets_the_source_value()
    {
        var recorder = new AsObject();
        Map(recorder, cfg => cfg.CreateMap<Source, Destination>().ForMember(nameof(Destination.Count), o => o.Condition<AsObject>()));
        recorder.SourceMember.ShouldBeNull();
        Map(recorder, cfg => cfg.CreateMap(typeof(Source), typeof(Destination)).ForMember(nameof(Destination.Number), o => o.Condition(typeof(AsObject))));
        recorder.SourceMember.ShouldBeOfType<int>().ShouldBe(42);
    }
    [Fact]
    public void Written_against_a_base_type_of_the_source_value_it_gets_the_source_value()
    {
        // The member is typed, List<string>; the class takes IEnumerable, which the source array is.
        var recorder = new AsEnumerable();
        var (source, destination) = Map(recorder, cfg => cfg.CreateMap<Source, Destination>().ForMember(d => d.Names, o => o.Condition<AsEnumerable>()));
        recorder.SourceMember.ShouldBeSameAs(source.Names);
        destination.Names.ShouldBe(["Ann"]);
    }
    [Fact]
    public void Written_against_an_interface_of_a_source_value_type_it_gets_the_source_value_boxed()
    {
        var recorder = new AsComparable();
        Map(recorder, cfg => cfg.CreateMap(typeof(Source), typeof(Destination)).ForMember(nameof(Destination.Number), o => o.Condition(typeof(AsComparable))));
        recorder.SourceMember.ShouldBeOfType<int>().ShouldBe(42);
    }
    [Fact]
    public void Written_against_the_type_of_the_source_value_it_gets_the_source_value()
    {
        var recorder = new AsInt();
        Map(recorder, cfg => cfg.CreateMap(typeof(Source), typeof(Destination)).ForMember(nameof(Destination.Number), o => o.Condition(typeof(AsInt))));
        recorder.SourceMember.ShouldBeOfType<int>().ShouldBe(42);
    }
    [Fact]
    public void Written_against_the_type_of_the_destination_member_it_gets_the_value_that_is_about_to_be_assigned()
    {
        var recorder = new AsLong();
        Map(recorder, cfg => cfg.CreateMap<Source, Destination>().ForMember(d => d.Number, o => o.Condition<AsLong>()));
        recorder.SourceMember.ShouldBeOfType<long>().ShouldBe(420);
    }
    [Fact]
    public void Written_against_a_type_the_source_value_must_be_converted_to_it_gets_the_value_that_is_about_to_be_assigned_converted()
    {
        var recorder = new AsDecimal();
        Map(recorder, cfg => cfg.CreateMap(typeof(Source), typeof(Destination)).ForMember(nameof(Destination.Number), o => o.Condition(typeof(AsDecimal))));
        recorder.SourceMember.ShouldBeOfType<decimal>().ShouldBe(420);
    }
}

// From here on: what did not change.

public class When_a_condition_declares_the_type_of_the_destination_member : AutoMapperSpecBase
{
    object _count, _text, _number, _items, _name, _child;
    Destination _destination;
    class Source
    {
        public int? Count { get; set; }
        public string Text { get; set; }
        public int Number { get; set; }
        public List<int> Items { get; set; }
        public string Name { get; set; }
        public Child Child { get; set; }
    }
    class Destination
    {
        public int Count { get; set; }
        public int Text { get; set; }
        public long Number { get; set; }
        public List<int> Items { get; set; }
        public string Name { get; set; }
        public ChildDto Child { get; set; }
    }
    class Child
    {
        public int Value { get; set; }
    }
    class ChildDto
    {
        public int Value { get; set; }
    }
    protected override MapperConfiguration CreateConfiguration() => new(cfg =>
    {
        cfg.CreateMap<Child, ChildDto>();
        cfg.CreateMap<Source, Destination>()
            .ForMember(d => d.Count, o => o.Condition((source, destination, sourceMember) =>
            {
                _count = sourceMember;
                return sourceMember != 0;
            }))
            .ForMember(d => d.Text, o => o.Condition((source, destination, sourceMember) =>
            {
                _text = sourceMember;
                return true;
            }))
            .ForMember(d => d.Number, o => o.Condition((source, destination, sourceMember, destinationMember) =>
            {
                _number = sourceMember;
                return true;
            }))
            .ForMember(d => d.Items, o => o.Condition((source, destination, sourceMember, destinationMember, context) =>
            {
                _items = sourceMember;
                return true;
            }))
            .ForMember(d => d.Name, o =>
            {
                o.AddTransform(name => name.Trim());
                o.Condition((source, destination, sourceMember) =>
                {
                    _name = sourceMember;
                    return true;
                });
            })
            .ForMember(d => d.Child, o => o.Condition((source, destination, sourceMember) =>
            {
                _child = sourceMember;
                return true;
            }));
    });
    protected override void Because_of() => Mapper.Map(new Source { Text = "12", Number = 42, Name = " Ann ", Child = new() { Value = 1 } }, _destination = new Destination { Count = 7 });
    // A typed ForMember condition gets the value that is about to be assigned, as in AutoMapper 14.
    [Fact]
    public void A_null_int_still_arrives_as_zero()
    {
        _count.ShouldBeOfType<int>().ShouldBe(0);
        _destination.Count.ShouldBe(7);
    }
    [Fact]
    public void A_string_that_goes_into_an_int_still_arrives_as_the_converted_int() => _text.ShouldBeOfType<int>().ShouldBe(12);
    [Fact]
    public void An_int_that_goes_into_a_long_still_arrives_as_a_long() => _number.ShouldBeOfType<long>().ShouldBe(42);
    [Fact]
    public void A_null_collection_still_arrives_as_the_empty_collection_it_is_mapped_to() => _items.ShouldBeOfType<List<int>>().ShouldBeEmpty();
    [Fact]
    public void A_value_still_arrives_with_the_value_transformers_applied() => _name.ShouldBe("Ann");
    [Fact]
    public void An_object_that_is_mapped_to_another_type_still_arrives_as_the_mapped_one() => _child.ShouldBeOfType<ChildDto>().Value.ShouldBe(1);
}

public class When_the_source_and_destination_members_have_the_same_type : AutoMapperSpecBase
{
    readonly Dictionary<string, object> _sourceMembers = [];
    readonly Source _source = new() { Number = 42, Text = new string('a', 3), Date = new DateTime(2026, 11, 19) };
    class Source
    {
        public int Number { get; set; }
        public int? Count { get; set; }
        public string Text { get; set; }
        public string Missing { get; set; }
        public DateTime Date { get; set; }
    }
    class Destination
    {
        public int Number { get; set; }
        public int? Count { get; set; }
        public string Text { get; set; }
        public string Missing { get; set; }
        public DateTime Date { get; set; }
    }
    protected override MapperConfiguration CreateConfiguration() => new(cfg =>
        cfg.CreateMap<Source, Destination>().ForAllMembers(o => o.Condition((source, destination, sourceMember) =>
        {
            _sourceMembers[o.DestinationMember.Name] = sourceMember;
            return true;
        })));
    protected override void Because_of() => Mapper.Map(_source, new Destination());
    // The value is not converted, so the condition gets the same value as before.
    [Fact]
    public void The_condition_gets_the_same_value_as_before()
    {
        _sourceMembers["Number"].ShouldBe(42);
        _sourceMembers["Count"].ShouldBeNull();
        _sourceMembers["Text"].ShouldBeSameAs(_source.Text);
        _sourceMembers["Missing"].ShouldBeNull();
        _sourceMembers["Date"].ShouldBe(new DateTime(2026, 11, 19));
    }
}

public class When_the_destination_member_is_of_type_object : AutoMapperSpecBase
{
    readonly Dictionary<string, object> _sourceMembers = [];
    class Source
    {
        public int? Count { get; set; }
        public int Number { get; set; }
        public string Text { get; set; }
    }
    class Destination
    {
        public object Count { get; set; }
        public object Number { get; set; }
        public object Text { get; set; }
    }
    protected override MapperConfiguration CreateConfiguration() => new(cfg =>
        cfg.CreateMap<Source, Destination>()
            .ForMember(d => d.Text, o => o.AddTransform(text => text + " transformed"))
            .ForAllMembers(o => o.Condition((source, destination, sourceMember) =>
            {
                _sourceMembers[o.DestinationMember.Name] = sourceMember;
                return true;
            })));
    protected override void Because_of() => Mapper.Map(new Source { Number = 42, Text = "text" }, new Destination());
    // The condition declares object and the destination member is object: it declares the type of the destination
    // member, so it gets the value that is about to be assigned, as before.
    [Fact]
    public void The_condition_gets_the_value_that_is_about_to_be_assigned_as_before()
    {
        _sourceMembers["Count"].ShouldBeNull();
        _sourceMembers["Number"].ShouldBe(42);
        _sourceMembers["Text"].ShouldBe("text transformed");
    }
}

public class When_a_member_has_a_null_substitute : AutoMapperSpecBase
{
    readonly Dictionary<string, object> _sourceMembers = [];
    Destination _destination;
    class Source
    {
        public int? Count { get; set; }
        public string Name { get; set; }
    }
    class Destination
    {
        public int Count { get; set; }
        public string Name { get; set; }
    }
    protected override MapperConfiguration CreateConfiguration() => new(cfg =>
        cfg.CreateMap<Source, Destination>()
            .ForMember(d => d.Count, o => o.NullSubstitute(5))
            .ForMember(d => d.Name, o => o.NullSubstitute("nobody"))
            .ForAllMembers(o => o.Condition((source, destination, sourceMember) =>
            {
                _sourceMembers[o.DestinationMember.Name] = sourceMember;
                return sourceMember != null;
            })));
    protected override void Because_of() => _destination = Mapper.Map(new Source(), new Destination { Count = 7, Name = "Ann" });
    // The substitute is part of resolving the value, so the condition never sees the null: as before.
    [Fact]
    public void The_condition_gets_the_substitute_and_not_null_as_before()
    {
        _sourceMembers["Count"].ShouldBe(5);
        _sourceMembers["Name"].ShouldBe("nobody");
    }
    [Fact]
    public void A_condition_that_skips_nulls_does_not_skip_the_member_as_before()
    {
        _destination.Count.ShouldBe(5);
        _destination.Name.ShouldBe("nobody");
    }
}

public class When_a_condition_that_gets_the_source_value_says_no : AutoMapperSpecBase
{
    readonly List<string> _steps = [];
    Destination _destination;
    class Source
    {
        public List<string> Steps;
        string _value;
        public string Value
        {
            get
            {
                Steps?.Add("resolved");
                return _value;
            }
            set => _value = value;
        }
    }
    class Destination
    {
        public int Value { get; set; }
    }
    protected override MapperConfiguration CreateConfiguration() => new(cfg =>
    {
        cfg.CreateMap<string, int>().ConvertUsing((source, destination) =>
        {
            _steps.Add("mapped");
            return int.Parse(source);
        });
        cfg.CreateMap<Source, Destination>().ForAllMembers(o => o.Condition((source, destination, sourceMember) =>
        {
            _steps.Add($"condition: {sourceMember}");
            return false;
        }));
    });
    protected override void Because_of() => _destination = Mapper.Map(new Source { Steps = _steps, Value = "12" }, new Destination { Value = 7 });
    // The value is still resolved and mapped before the condition is evaluated; only what the condition is given changed.
    [Fact]
    public void The_member_is_resolved_and_mapped_before_the_condition_is_evaluated_as_before() =>
        _steps.ShouldBe(["resolved", "mapped", "condition: 12"]);
    [Fact]
    public void The_member_is_not_assigned() => _destination.Value.ShouldBe(7);
}

public class When_a_path_has_a_condition : AutoMapperSpecBase
{
    object _sourceMember = "not evaluated";
    class Source
    {
        public int? Count { get; set; }
    }
    class Destination
    {
        public Inner Inner { get; set; } = new();
    }
    class Inner
    {
        public int Count { get; set; }
    }
    protected override MapperConfiguration CreateConfiguration() => new(cfg =>
        cfg.CreateMap<Source, Destination>().ForPath(d => d.Inner.Count, o =>
        {
            o.MapFrom(s => s.Count);
            o.Condition(parameters =>
            {
                _sourceMember = parameters.SourceMember;
                return true;
            });
        }));
    protected override void Because_of() => Mapper.Map(new Source(), new Destination());
    // A ForPath condition declares the type of the destination member, like a typed ForMember condition.
    [Fact]
    public void A_null_int_still_arrives_as_zero() => _sourceMember.ShouldBe(0);
}
