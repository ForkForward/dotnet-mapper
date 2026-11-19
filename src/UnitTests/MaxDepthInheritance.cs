namespace ForkForward.Mapper.UnitTests;

// A derived map used to ignore the MaxDepth of the map it inherits from: with MaxDepth on the base map only, a
// graph of derived objects was mapped to any depth. A derived map that sets no MaxDepth of its own now takes the
// one of the map it inherits from, through Include, IncludeBase and IncludeAllDerived; its own MaxDepth still wins.
public class MaxDepthInheritance
{
    public class Base
    {
        public int Value { get; set; }
        public Base Next { get; set; }
    }
    public class Derived : Base { }
    public class MoreDerived : Derived { }
    public class BaseDto
    {
        public int Value { get; set; }
        public BaseDto Next { get; set; }
    }
    public class DerivedDto : BaseDto { }
    public class MoreDerivedDto : DerivedDto { }

    [Fact]
    public void A_derived_map_takes_MaxDepth_from_the_map_that_includes_it()
    {
        var mapper = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Base, BaseDto>().Include<Derived, DerivedDto>().MaxDepth(10);
            cfg.CreateMap<Derived, DerivedDto>();
        }).CreateMapper();

        var destination = mapper.Map<BaseDto>(Chain<Derived>(100));

        // Before: 100 levels.
        Levels(destination).ShouldBe(10);
        destination.ShouldBeOfType<DerivedDto>();
    }

    [Fact]
    public void A_derived_map_takes_MaxDepth_from_the_map_it_names_with_IncludeBase()
    {
        var mapper = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Base, BaseDto>().MaxDepth(10);
            cfg.CreateMap<Derived, DerivedDto>().IncludeBase<Base, BaseDto>();
        }).CreateMapper();

        // Before: 100 levels.
        Levels(mapper.Map<BaseDto>(Chain<Derived>(100))).ShouldBe(10);
    }

    [Fact]
    public void A_derived_map_takes_MaxDepth_from_a_map_with_IncludeAllDerived()
    {
        var mapper = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Base, BaseDto>().IncludeAllDerived().MaxDepth(10);
            cfg.CreateMap<Derived, DerivedDto>();
        }).CreateMapper();

        // Before: 100 levels.
        Levels(mapper.Map<BaseDto>(Chain<Derived>(100))).ShouldBe(10);
    }

    [Fact]
    public void The_MaxDepth_a_derived_map_sets_itself_wins_over_the_inherited_one()
    {
        var mapper = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Base, BaseDto>().Include<Derived, DerivedDto>().MaxDepth(10);
            cfg.CreateMap<Derived, DerivedDto>().MaxDepth(3);
        }).CreateMapper();

        // Unchanged: each map has its own limit.
        Levels(mapper.Map<BaseDto>(Chain<Derived>(100))).ShouldBe(3);
        Levels(mapper.Map<BaseDto>(Chain<Base>(100))).ShouldBe(10);
    }

    [Fact]
    public void A_derived_map_stays_without_a_limit_when_it_sets_one_that_no_graph_reaches()
    {
        var mapper = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Base, BaseDto>().Include<Derived, DerivedDto>().MaxDepth(10);
            cfg.CreateMap<Derived, DerivedDto>().MaxDepth(int.MaxValue);
        }).CreateMapper();

        // The way to keep what a derived map without MaxDepth did before: all 100 levels.
        Levels(mapper.Map<BaseDto>(Chain<Derived>(100))).ShouldBe(100);
    }

    [Fact]
    public void MaxDepth_is_inherited_through_several_levels()
    {
        var mapper = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Base, BaseDto>().MaxDepth(10);
            cfg.CreateMap<Derived, DerivedDto>().IncludeBase<Base, BaseDto>();
            cfg.CreateMap<MoreDerived, MoreDerivedDto>().IncludeBase<Derived, DerivedDto>();
        }).CreateMapper();

        // Before: 100 levels.
        Levels(mapper.Map<BaseDto>(Chain<MoreDerived>(100))).ShouldBe(10);
    }

    [Fact]
    public void The_nearest_map_that_sets_MaxDepth_is_the_one_inherited_from()
    {
        var mapper = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Base, BaseDto>().MaxDepth(10);
            cfg.CreateMap<Derived, DerivedDto>().IncludeBase<Base, BaseDto>().MaxDepth(5);
            cfg.CreateMap<MoreDerived, MoreDerivedDto>().IncludeBase<Derived, DerivedDto>();
        }).CreateMapper();

        // Before: 100 levels.
        Levels(mapper.Map<BaseDto>(Chain<MoreDerived>(100))).ShouldBe(5);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_nearest_map_wins_also_when_every_base_map_includes_the_derived_map_directly(bool baseMapFirst)
    {
        // The most derived map inherits from both other maps directly, in the order in which they were created.
        var mapper = new MapperConfiguration(cfg =>
        {
            if (baseMapFirst)
            {
                cfg.CreateMap<Base, BaseDto>().IncludeAllDerived().MaxDepth(10);
                cfg.CreateMap<Derived, DerivedDto>().IncludeAllDerived().MaxDepth(5);
            }
            else
            {
                cfg.CreateMap<Derived, DerivedDto>().IncludeAllDerived().MaxDepth(5);
                cfg.CreateMap<Base, BaseDto>().IncludeAllDerived().MaxDepth(10);
            }
            cfg.CreateMap<MoreDerived, MoreDerivedDto>();
        }).CreateMapper();

        // Before: 100 levels.
        Levels(mapper.Map<BaseDto>(Chain<MoreDerived>(100))).ShouldBe(5);
    }

    [Fact]
    public void Each_map_counts_its_own_depth_so_a_chain_that_alternates_base_and_derived_objects_gets_twice_as_deep()
    {
        var mapper = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Base, BaseDto>().Include<Derived, DerivedDto>().MaxDepth(10);
            cfg.CreateMap<Derived, DerivedDto>();
        }).CreateMapper();
        var source = Chain(100, value => value % 2 == 0 ? new Base() : new Derived());

        var destination = mapper.Map<BaseDto>(source);

        // The limit is inherited, the count is not shared: 10 levels of the base map and 10 of the derived map.
        // Before it was 20 as well, for another reason: only the base objects were counted.
        Levels(destination).ShouldBe(20);
    }

    [Fact]
    public void ReverseMap_does_not_carry_MaxDepth_over_to_the_reverse_map()
    {
        var mapper = new MapperConfiguration(cfg => cfg.CreateMap<Base, BaseDto>().MaxDepth(10).ReverseMap()).CreateMapper();
        BaseDto dtos = null;
        for (var value = 49; value >= 0; value--)
        {
            dtos = new BaseDto { Value = value, Next = dtos };
        }

        // Unchanged: the limit belongs to the map it was set on. The reverse map has none, unless MaxDepth is
        // called again after ReverseMap.
        Levels(mapper.Map<BaseDto>(Chain<Base>(50))).ShouldBe(10);
        var levels = 0;
        for (var node = mapper.Map<Base>(dtos); node != null; node = node.Next)
        {
            levels++;
        }
        levels.ShouldBe(50);
    }

    public class Animal
    {
        public string Name { get; set; }
    }
    public class Dog : Animal { }
    public class AnimalDto
    {
        public string Name { get; set; }
    }
    public class DogDto : AnimalDto { }
    public class Owner
    {
        public Dog First { get; set; }
        public Dog Second { get; set; }
        public Animal Third { get; set; }
    }
    public class OwnerDto
    {
        public DogDto First { get; set; }
        public DogDto Second { get; set; }
        public AnimalDto Third { get; set; }
    }

    [Fact]
    public void In_a_projection_a_derived_map_does_not_inherit_MaxDepth()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Animal, AnimalDto>().Include<Dog, DogDto>().MaxDepth(1);
            cfg.CreateMap<Dog, DogDto>();
            cfg.CreateMap<Owner, OwnerDto>();
        });
        var owners = new[] { new Owner { First = new Dog { Name = "first" }, Second = new Dog { Name = "second" }, Third = new Dog { Name = "third" } } };

        var owner = owners.AsQueryable().ProjectTo<OwnerDto>(configuration).Single();

        // Unchanged. In a projection MaxDepth counts how many times a map is used in the query, not how deep the
        // graph is: with the limit of the base map, the derived map would project its first use only, and the
        // second and third dog would be missing from a projection that has always been complete.
        owner.First.Name.ShouldBe("first");
        owner.Second.Name.ShouldBe("second");
        owner.Third.ShouldBeOfType<DogDto>().Name.ShouldBe("third");
    }

    static Base Chain<T>(int length) where T : Base, new() => Chain(length, _ => new T());

    static Base Chain(int length, Func<int, Base> create)
    {
        Base head = null;
        for (var value = length - 1; value >= 0; value--)
        {
            var node = create(value);
            node.Value = value;
            node.Next = head;
            head = node;
        }
        return head;
    }

    static int Levels(BaseDto head)
    {
        var levels = 0;
        for (var node = head; node != null; node = node.Next)
        {
            levels.ShouldBe(node.Value);
            levels++;
        }
        return levels;
    }
}
