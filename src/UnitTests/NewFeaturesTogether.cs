namespace ForkForward.Mapper.UnitTests;

// Each feature that came out of the comparison with the newer AutoMapper versions
// (docs/analysis/upstream-gap-analysis-2026-11.md) has a test file of its own. The tests here use several of
// them in one configuration, at the places where one could get in the way of another.
public class NewFeaturesTogether
{
    public class SkipNulls : IMemberCondition<object, object, object>
    {
        public bool ShouldMap(object source, object destination, object sourceMember, object destinationMember, ResolutionContext context) =>
            sourceMember != null;
    }
    public class Inner
    {
        public int Number { get; set; }
    }
    public class PatchSource
    {
        public int? Count { get; set; }
        public Inner Inner { get; set; }
        public List<string> Tags { get; set; }
        public string Name { get; set; }
    }
    public class PatchDestination
    {
        public int Count { get; set; }
        public int? InnerNumber { get; set; }
        public List<string> Tags { get; set; }
        public string Name { get; set; }
    }

    // A condition class, the source value instead of the converted one, and a member mapped at run time into a
    // nullable (which did not even build a configuration before): the "apply only what was sent" map.
    [Fact]
    public void A_condition_class_that_skips_nulls_leaves_every_kind_of_member_as_it_is()
    {
        var configuration = new MapperConfiguration(cfg => cfg.CreateMap<PatchSource, PatchDestination>()
            .ForMember(d => d.InnerNumber, o => o.MapAtRuntime())
            .ForAllMembers(o => o.Condition<SkipNulls>()));
        configuration.AssertConfigurationIsValid();
        var mapper = configuration.CreateMapper();
        var tags = new List<string> { "kept" };
        var destination = new PatchDestination { Count = 7, InnerNumber = 8, Tags = tags, Name = "kept" };

        mapper.Map(new PatchSource(), destination);

        // int? without a value into int: the condition used to get 0, and Count was overwritten with it.
        destination.Count.ShouldBe(7);
        // Inner.Number flattened into int?, with Inner null.
        destination.InnerNumber.ShouldBe(8);
        // A null collection: the condition used to get the empty list it is mapped to, and Tags was replaced.
        destination.Tags.ShouldBeSameAs(tags);
        destination.Tags.ShouldBe(["kept"]);
        destination.Name.ShouldBe("kept");

        mapper.Map(new PatchSource { Count = 1, Inner = new() { Number = 2 }, Tags = ["new"], Name = "new" }, destination);

        destination.Count.ShouldBe(1);
        destination.InnerNumber.ShouldBe(2);
        destination.Tags.ShouldBe(["new"]);
        destination.Name.ShouldBe("new");
    }

    public class Measured
    {
        public int Amount { get; set; }
    }
    public class SameTypeDto
    {
        int _amount, _assignments;
        public int Amount
        {
            get => _amount;
            set
            {
                _amount = value;
                _assignments++;
            }
        }
        public int Assignments() => _assignments;
    }
    public class WiderTypeDto
    {
        long _amount;
        int _assignments;
        public long Amount
        {
            get => _amount;
            set
            {
                _amount = value;
                _assignments++;
            }
        }
        public int Assignments() => _assignments;
    }

    // A side of "the condition gets the source value" that is easy to miss: a condition that skips a value that
    // has not changed compares the source member with the destination member. When the two members differ in type
    // it used to compare two values of the destination type; now it compares an int with a long, which are never
    // equal as objects, and the member is assigned the value it already had.
    [Fact]
    public void A_condition_that_skips_unchanged_values_no_longer_skips_when_the_members_differ_in_type()
    {
        var mapper = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Measured, SameTypeDto>().ForAllMembers(o => o.Condition((s, d, sourceMember, destinationMember) => !Equals(sourceMember, destinationMember)));
            cfg.CreateMap<Measured, WiderTypeDto>().ForAllMembers(o => o.Condition((s, d, sourceMember, destinationMember) => !Equals(sourceMember, destinationMember)));
        }).CreateMapper();
        var sameType = new SameTypeDto { Amount = 5 };
        var widerType = new WiderTypeDto { Amount = 5 };

        mapper.Map(new Measured { Amount = 5 }, sameType);
        mapper.Map(new Measured { Amount = 5 }, widerType);

        // int and int: skipped, as before. Only the initializer assigned it.
        sameType.Assignments().ShouldBe(1);
        // int and long: assigned again. In AutoMapper 14 the condition got the long 5 and skipped it.
        widerType.Assignments().ShouldBe(2);
        widerType.Amount.ShouldBe(5);
    }

    public class ForStrings : IMemberCondition<object, object, string>
    {
        public bool ShouldMap(object source, object destination, string sourceMember, string destinationMember, ResolutionContext context) => true;
    }
    public class Numbers
    {
        public int Value { get; set; }
    }
    public class NumbersDto
    {
        public int Value { get; set; }
    }

    // A condition class given as a type is checked for the interface, not for the member it is used on. When it
    // does not fit the member, building the plan fails, and that error now names the map and the member.
    [Fact]
    public void A_condition_class_for_another_member_type_fails_with_the_map_and_the_member()
    {
        var error = Should.Throw<AutoMapperMappingException>(() => new MapperConfiguration(cfg =>
            cfg.CreateMap(typeof(Numbers), typeof(NumbersDto)).ForMember(nameof(NumbersDto.Value), o => o.Condition(typeof(ForStrings)))));

        error.Message.ShouldStartWith("Error building the mapping plan.");
        error.TypeMap.Types.ShouldBe(new TypePair(typeof(Numbers), typeof(NumbersDto)));
        error.MemberMap.DestinationName.ShouldBe(nameof(NumbersDto.Value));
        error.InnerException.ShouldNotBeNull();
    }

    public class Node
    {
        public int Value { get; set; }
        public Node Next { get; set; }
    }
    public class NodeDto
    {
        public int Value { get; set; }
        public NodeDto Next { get; set; }
    }
    // Creates the next level itself, by mapping it through the context.
    public class NodeDtoFactory : IDestinationFactory<Node, NodeDto>
    {
        public NodeDto Create(Node source, ResolutionContext context) =>
            new() { Next = source.Next == null ? null : context.Mapper.Map<NodeDto>(source.Next) };
    }
    static Node Chain(int length)
    {
        Node node = null;
        for (var value = length - 1; value >= 0; value--)
        {
            node = new() { Value = value, Next = node };
        }
        return node;
    }
    static int Length(NodeDto node)
    {
        var length = 0;
        for (; node != null; node = node.Next)
        {
            length++;
        }
        return length;
    }

    // MaxDepth is counted before the destination is created, so it also holds for levels a factory class maps.
    [Fact]
    public void MaxDepth_stops_a_graph_whose_levels_are_mapped_by_a_destination_factory()
    {
        var throughProperty = new MapperConfiguration(cfg => cfg.CreateMap<Node, NodeDto>().MaxDepth(10)).CreateMapper();
        var throughFactory = new MapperConfiguration(cfg => cfg.CreateMap<Node, NodeDto>()
            .ConstructUsing<NodeDtoFactory>().ForMember(d => d.Next, o => o.Ignore()).MaxDepth(10)).CreateMapper();

        var expected = Length(throughProperty.Map<NodeDto>(Chain(100)));

        expected.ShouldBe(10);
        Length(throughFactory.Map<NodeDto>(Chain(100))).ShouldBe(expected);
    }

    // The same route without MaxDepth, with a graph that does not fit on the stack: the stack check stops it.
    [Fact]
    public void A_graph_too_deep_for_the_stack_ends_in_an_exception_when_a_destination_factory_maps_its_levels()
    {
        var mapper = new MapperConfiguration(cfg => cfg.CreateMap<Node, NodeDto>()
            .ConstructUsing<NodeDtoFactory>().ForMember(d => d.Next, o => o.Ignore())).CreateMapper();
        var source = Chain(100_000);
        Exception exception = null;
        Thread thread = new(() =>
        {
            try
            {
                mapper.Map<NodeDto>(source);
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        }, 256 * 1024);
        thread.Start();
        thread.Join();

        exception.ShouldBeAssignableTo<AutoMapperMappingException>().Message.ShouldContain("nested too deeply", Case.Sensitive);
    }

    public class Animal
    {
        public string Name { get; set; }
        public Animal Parent { get; set; }
    }
    public class Cat : Animal
    {
    }
    public class AnimalDto
    {
        public string Name { get; set; }
        public AnimalDto Parent { get; set; }
        public string CreatedBy { get; set; }
    }
    public class CatDto : AnimalDto
    {
    }
    public class AnimalDtoFactory : IDestinationFactory<Animal, AnimalDto>
    {
        public AnimalDto Create(Animal source, ResolutionContext context) => new() { CreatedBy = "factory" };
    }
    public class NotSecret : IMemberCondition<Animal, AnimalDto, string>
    {
        public bool ShouldMap(Animal source, AnimalDto destination, string sourceMember, string destinationMember, ResolutionContext context) =>
            sourceMember != "secret";
    }
    static Animal Cats(int length, string name)
    {
        Animal cat = null;
        for (var index = 0; index < length; index++)
        {
            cat = new Cat { Name = name, Parent = cat };
        }
        return cat;
    }
    static int Length(AnimalDto animal)
    {
        var length = 0;
        for (; animal != null; animal = animal.Parent)
        {
            length++;
        }
        return length;
    }

    // What a derived map takes from the map that includes it, and what it does not.
    [Fact]
    public void A_derived_map_inherits_the_condition_class_and_MaxDepth_and_not_the_destination_factory()
    {
        var mapper = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Animal, AnimalDto>()
                .Include<Cat, CatDto>()
                .ConstructUsing<AnimalDtoFactory>()
                .MaxDepth(3)
                .ForMember(d => d.CreatedBy, o => o.Ignore())
                .ForMember(d => d.Name, o => o.Condition<NotSecret>());
            cfg.CreateMap<Cat, CatDto>();
        }).CreateMapper();

        var animal = mapper.Map<AnimalDto>(new Animal { Name = "Rex" });
        animal.ShouldBeOfType<AnimalDto>();
        animal.CreatedBy.ShouldBe("factory");

        var cat = mapper.Map<AnimalDto>(Cats(10, "Tom"));
        cat.ShouldBeOfType<CatDto>();
        cat.CreatedBy.ShouldBeNull();
        cat.Name.ShouldBe("Tom");
        Length(cat).ShouldBe(3);

        mapper.Map<AnimalDto>(Cats(1, "secret")).Name.ShouldBeNull();
    }

    public class FooBar
    {
    }
    public class SourceBase<T> where T : class
    {
        public string Text { get; set; }
    }
    public class SourceDerived : SourceBase<FooBar>
    {
    }
    public class DestinationBase
    {
        public string Text { get; set; }
    }
    public class DestinationDerived : DestinationBase
    {
    }
    public class NotEmpty : IMemberCondition<object, object, string>
    {
        public bool ShouldMap(object source, object destination, string sourceMember, string destinationMember, ResolutionContext context) =>
            !string.IsNullOrEmpty(sourceMember);
    }

    // The order in which an open generic map and the map that includes it are registered does not matter, also
    // for a condition class configured on the open generic map.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_condition_class_on_an_open_generic_base_map_is_inherited_in_either_registration_order(bool openGenericMapFirst)
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            if (openGenericMapFirst)
            {
                OpenGenericMap();
            }
            cfg.CreateMap<SourceDerived, DestinationDerived>().IncludeBase<SourceBase<FooBar>, DestinationBase>();
            if (!openGenericMapFirst)
            {
                OpenGenericMap();
            }
            void OpenGenericMap() => cfg.CreateMap(typeof(SourceBase<>), typeof(DestinationBase))
                .ForMember(nameof(DestinationBase.Text), o => o.Condition(typeof(NotEmpty)));
        });
        configuration.AssertConfigurationIsValid();
        var mapper = configuration.CreateMapper();

        mapper.Map(new SourceDerived { Text = "" }, new DestinationDerived { Text = "kept" }).Text.ShouldBe("kept");
        mapper.Map(new SourceDerived { Text = "new" }, new DestinationDerived { Text = "kept" }).Text.ShouldBe("new");
        mapper.Map(new SourceBase<FooBar> { Text = "" }, new DestinationBase { Text = "kept" }).Text.ShouldBe("kept");
    }

    public class Category
    {
        public string Name { get; set; }
        public Category Parent { get; set; }
    }
    public class SpecialCategory : Category
    {
        public int Rank { get; set; }
    }
    public class CategoryDto
    {
        public string Name { get; set; }
        public CategoryDto Parent { get; set; }
    }
    public class SpecialCategoryDto : CategoryDto
    {
        public int Rank { get; set; }
    }

    // With polymorphic projection off, a base map that has MaxDepth and derived maps is projected with that map
    // alone, down to its MaxDepth, and no derived type appears at any level.
    [Fact]
    public void A_base_map_with_MaxDepth_is_projected_on_its_own_when_polymorphic_projection_is_off()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.Internal().RecursiveQueriesMaxDepth = 5;
            cfg.CreateMap<Category, CategoryDto>().Include<SpecialCategory, SpecialCategoryDto>().MaxDepth(2).PolymorphicProjection(false);
            cfg.CreateMap<SpecialCategory, SpecialCategoryDto>();
        });
        Category[] categories = [new SpecialCategory { Name = "leaf", Rank = 1, Parent = new SpecialCategory { Name = "middle", Parent = new Category { Name = "root" } } }];

        var projected = categories.AsQueryable().ProjectTo<CategoryDto>(configuration).Single();

        projected.ShouldBeOfType<CategoryDto>();
        projected.Name.ShouldBe("leaf");
        for (var category = projected.Parent; category != null; category = category.Parent)
        {
            category.ShouldBeOfType<CategoryDto>();
        }
        // In-memory mapping is not affected by the switch.
        configuration.CreateMapper().Map<CategoryDto>(categories[0]).ShouldBeOfType<SpecialCategoryDto>().Rank.ShouldBe(1);
    }
}
