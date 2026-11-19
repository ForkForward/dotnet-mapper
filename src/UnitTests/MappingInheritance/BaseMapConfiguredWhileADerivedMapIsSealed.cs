namespace ForkForward.Mapper.UnitTests.MappingInheritance;
/// <summary>
/// A closed generic map that an include asks for is sealed while the profiles are still being configured, and the maps it will seal with
/// it are configured first. Sealing one of them can build a plan that asks for another closed generic map, whose base map is then configured
/// before its turn, and configuring a base map adds it to the inherited maps of its derived maps: also to one that is being sealed at that
/// moment. Reading that list with an enumerator ended in "Collection was modified" from the MapperConfiguration constructor, in half of the
/// orders in which these maps can be registered, for configurations that 14.1.1 built in every order. Found in review.
/// </summary>
public class BaseMapConfiguredWhileADerivedMapIsSealed : InEveryRegistrationOrder
{
    public class Root
    {
    }
    public abstract class RootDto
    {
    }
    public class Upper : Root
    {
        public Holder<int> Inner { get; set; }
    }
    public abstract class UpperDto : RootDto
    {
        public HolderDto<int> Inner { get; set; }
    }
    public class Lower : Upper
    {
    }
    public abstract class LowerDto : UpperDto
    {
    }
    public class Entity<T> : Lower
    {
    }
    public class EntityDto<T> : LowerDto
    {
    }
    public class Holder<T> : Root
    {
        public T Value { get; set; }
    }
    public class HolderDto<T> : RootDto
    {
        public T Value { get; set; }
    }
    public class Trigger : Entity<int>
    {
    }
    public class TriggerDto : EntityDto<int>
    {
    }

    static Action<IProfileExpression>[] Maps(Action<IProfileExpression> rootMap) =>
    [
        cfg => cfg.CreateMap<Trigger, TriggerDto>().IncludeBase<Entity<int>, EntityDto<int>>(),
        cfg => cfg.CreateMap(typeof(Entity<>), typeof(EntityDto<>)).IncludeBase(typeof(Lower), typeof(LowerDto)),
        cfg => cfg.CreateMap<Lower, LowerDto>().IncludeBase<Upper, UpperDto>(),
        cfg => cfg.CreateMap<Upper, UpperDto>(),
        cfg => cfg.CreateMap(typeof(Holder<>), typeof(HolderDto<>)).IncludeBase(typeof(Root), typeof(RootDto)),
        rootMap,
    ];

    static void Check(IMapper mapper)
    {
        var destination = mapper.Map<TriggerDto>(new Trigger { Inner = new() { Value = 7 } });
        destination.Inner.ShouldBeOfType<HolderDto<int>>().Value.ShouldBe(7);
    }

    [Fact]
    public void A_root_map_that_includes_all_derived_maps() => InEveryOrder(Maps(cfg => cfg.CreateMap<Root, RootDto>().IncludeAllDerived()), Check);

    [Fact]
    public void A_root_map_that_includes_the_closed_generic_map_of_the_entity() =>
        InEveryOrder(Maps(cfg => cfg.CreateMap<Root, RootDto>().Include<Entity<int>, EntityDto<int>>()), Check);

}
