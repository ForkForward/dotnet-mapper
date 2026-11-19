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

    public class ChainRoot
    {
        public ChainLeaf Child { get; set; }
    }
    public class ChainMiddle : ChainRoot
    {
    }
    public class ChainLeaf : ChainMiddle
    {
    }
    public class ChainRootDto
    {
        public ChainLeafDto Child { get; set; }
    }
    public class ChainMiddleDto : ChainRootDto
    {
    }
    public class ChainLeafDto : ChainMiddleDto
    {
    }

    // The map in the middle may still be in the middle of its own sealing when the map below it asks it for its
    // MaxDepth: the root map has a member of the leaf type, so sealing the root seals the leaf. Which map is sealed
    // first depends on the order they were registered in, and the leaf must get the limit in every order.
    [Theory]
    [InlineData(0, 1, 2)]
    [InlineData(0, 2, 1)]
    [InlineData(1, 0, 2)]
    [InlineData(1, 2, 0)]
    [InlineData(2, 0, 1)]
    [InlineData(2, 1, 0)]
    public void Through_a_map_in_between_in_every_registration_order(int first, int second, int third)
    {
        Action<IMapperConfigurationExpression>[] maps =
        [
            cfg => cfg.CreateMap<ChainRoot, ChainRootDto>().MaxDepth(3),
            cfg => cfg.CreateMap<ChainMiddle, ChainMiddleDto>().IncludeBase<ChainRoot, ChainRootDto>(),
            cfg => cfg.CreateMap<ChainLeaf, ChainLeafDto>().IncludeBase<ChainMiddle, ChainMiddleDto>(),
        ];
        var configuration = new MapperConfiguration(cfg =>
        {
            maps[first](cfg);
            maps[second](cfg);
            maps[third](cfg);
        });
        ChainLeaf leaf = null;
        for (var index = 0; index < 10; index++)
        {
            leaf = new() { Child = leaf };
        }

        var levels = 0;
        for (ChainRootDto node = configuration.CreateMapper().Map<ChainRootDto>(leaf); node != null; node = node.Child)
        {
            node.ShouldBeOfType<ChainLeafDto>();
            levels++;
        }

        configuration.FindTypeMapFor<ChainMiddle, ChainMiddleDto>().MaxDepth.ShouldBe(3);
        configuration.FindTypeMapFor<ChainLeaf, ChainLeafDto>().MaxDepth.ShouldBe(3);
        levels.ShouldBe(3);
    }

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

    // A map can inherit from several maps that have a MaxDepth. Which of them it takes must not depend on the order
    // in which they were included or registered:
    //   1. a more derived map beats a less derived one;
    //   2. of the maps that are left, one that set its MaxDepth itself beats one that inherited it;
    //   3. of those that are still left, the smallest MaxDepth wins.
    public interface IRoot { }
    public interface ILeft { }
    public interface IRight : IRoot { }
    public class Both : ILeft, IRight
    {
        public Both Next { get; set; }
    }
    public class MoreThanBoth : Both { }
    public interface IRootDto { }
    public interface ILeftDto { }
    public interface IRightDto : IRootDto { }
    public class BothDto : ILeftDto, IRightDto
    {
        public BothDto Next { get; set; }
    }
    public class MoreThanBothDto : BothDto { }

    static int LevelsOf<TDestination>(IMapper mapper, int maxDepthOfTheMap) where TDestination : BothDto
    {
        Both source = null;
        for (var index = 0; index < 20; index++)
        {
            source = typeof(TDestination) == typeof(BothDto) ? new Both { Next = source } : new MoreThanBoth { Next = source };
        }
        mapper.ConfigurationProvider.Internal().FindTypeMapFor(source.GetType(), typeof(TDestination)).MaxDepth.ShouldBe(maxDepthOfTheMap);
        var levels = 0;
        for (BothDto node = mapper.Map<TDestination>(source); node != null; node = node.Next)
        {
            node.ShouldBeOfType<TDestination>();
            levels++;
        }
        return levels;
    }

    [Theory]
    [InlineData(5, 2, true)]
    [InlineData(5, 2, false)]
    [InlineData(2, 5, true)]
    [InlineData(2, 5, false)]
    public void Of_two_base_maps_that_are_not_related_the_smaller_MaxDepth_wins_in_either_order_of_IncludeBase(int left, int right, bool leftFirst)
    {
        var mapper = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<ILeft, ILeftDto>().MaxDepth(left);
            cfg.CreateMap<IRight, IRightDto>().MaxDepth(right);
            var both = cfg.CreateMap<Both, BothDto>();
            _ = leftFirst ?
                both.IncludeBase<ILeft, ILeftDto>().IncludeBase<IRight, IRightDto>() :
                both.IncludeBase<IRight, IRightDto>().IncludeBase<ILeft, ILeftDto>();
        }).CreateMapper();

        // Before: the MaxDepth of the map that was included first, 5 in two of the four cases.
        LevelsOf<BothDto>(mapper, 2).ShouldBe(2);
    }

    [Theory]
    [InlineData(5, 2, true)]
    [InlineData(5, 2, false)]
    [InlineData(2, 5, true)]
    [InlineData(2, 5, false)]
    public void Of_two_base_maps_that_are_not_related_the_smaller_MaxDepth_wins_in_either_order_of_registration(int left, int right, bool leftFirst)
    {
        var mapper = new MapperConfiguration(cfg =>
        {
            if (leftFirst)
            {
                cfg.CreateMap<ILeft, ILeftDto>().Include<Both, BothDto>().MaxDepth(left);
            }
            cfg.CreateMap<IRight, IRightDto>().Include<Both, BothDto>().MaxDepth(right);
            if (!leftFirst)
            {
                cfg.CreateMap<ILeft, ILeftDto>().Include<Both, BothDto>().MaxDepth(left);
            }
            cfg.CreateMap<Both, BothDto>();
        }).CreateMapper();

        // Before: the MaxDepth of the base map that was registered first.
        LevelsOf<BothDto>(mapper, 2).ShouldBe(2);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_MaxDepth_that_a_base_map_set_itself_beats_one_that_an_unrelated_base_map_inherited(bool leftFirst)
    {
        var mapper = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<IRoot, IRootDto>().MaxDepth(2);
            // Sets none itself: it has the 2 of the map above.
            cfg.CreateMap<IRight, IRightDto>().IncludeBase<IRoot, IRootDto>();
            cfg.CreateMap<ILeft, ILeftDto>().MaxDepth(5);
            var both = cfg.CreateMap<Both, BothDto>();
            _ = leftFirst ?
                both.IncludeBase<ILeft, ILeftDto>().IncludeBase<IRight, IRightDto>() :
                both.IncludeBase<IRight, IRightDto>().IncludeBase<ILeft, ILeftDto>();
        }).CreateMapper();

        // Before: 2 when the map that had only inherited its MaxDepth was included first.
        LevelsOf<BothDto>(mapper, 5).ShouldBe(5);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_more_derived_base_map_beats_a_less_derived_one_also_when_it_inherited_its_MaxDepth(bool lessDerivedFirst)
    {
        var mapper = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<IRoot, IRootDto>().MaxDepth(2);
            cfg.CreateMap<ILeft, ILeftDto>().MaxDepth(5);
            // Sets none itself: it has the 2 of the first map, which is not related to the second.
            cfg.CreateMap<Both, BothDto>().IncludeBase<IRoot, IRootDto>();
            var moreThanBoth = cfg.CreateMap<MoreThanBoth, MoreThanBothDto>();
            _ = lessDerivedFirst ?
                moreThanBoth.IncludeBase<ILeft, ILeftDto>().IncludeBase<Both, BothDto>() :
                moreThanBoth.IncludeBase<Both, BothDto>().IncludeBase<ILeft, ILeftDto>();
        }).CreateMapper();

        // Unchanged: among maps that are related, the most derived one has always won, in either order.
        LevelsOf<MoreThanBothDto>(mapper, 2).ShouldBe(2);
    }

    public class OpenRoot<T>
    {
        public OpenLeaf Next { get; set; }
    }
    public class OpenMiddle<T> : OpenRoot<T> { }
    public class OpenLeaf : OpenMiddle<int> { }
    public class OpenRootDto<T>
    {
        public OpenLeafDto Next { get; set; }
    }
    public class OpenMiddleDto<T> : OpenRootDto<T> { }
    public class OpenLeafDto : OpenMiddleDto<int> { }

    // The types of an open generic map are type definitions, and Type.IsAssignableFrom says no to those: the two base
    // maps looked unrelated, and the one that was included first won.
    [Theory]
    [InlineData(2, 5, true)]
    [InlineData(2, 5, false)]
    [InlineData(5, 2, true)]
    [InlineData(5, 2, false)]
    public void Of_two_open_generic_base_maps_the_more_derived_one_wins_in_either_order_of_IncludeBase(int root, int middle, bool rootFirst)
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap(typeof(OpenRoot<>), typeof(OpenRootDto<>)).MaxDepth(root);
            cfg.CreateMap(typeof(OpenMiddle<>), typeof(OpenMiddleDto<>)).MaxDepth(middle);
            var leaf = cfg.CreateMap<OpenLeaf, OpenLeafDto>();
            _ = rootFirst ?
                leaf.IncludeBase(typeof(OpenRoot<>), typeof(OpenRootDto<>)).IncludeBase(typeof(OpenMiddle<>), typeof(OpenMiddleDto<>)) :
                leaf.IncludeBase(typeof(OpenMiddle<>), typeof(OpenMiddleDto<>)).IncludeBase(typeof(OpenRoot<>), typeof(OpenRootDto<>));
        });
        OpenLeaf source = null;
        for (var index = 0; index < 20; index++)
        {
            source = new() { Next = source };
        }

        var levels = 0;
        for (var node = configuration.CreateMapper().Map<OpenLeafDto>(source); node != null; node = node.Next)
        {
            levels++;
        }

        // Before: the MaxDepth of the map that was included first.
        configuration.FindTypeMapFor<OpenLeaf, OpenLeafDto>().MaxDepth.ShouldBe(middle);
        levels.ShouldBe(middle);
    }
}
