using ForkForward.Mapper.UnitTests.MappingInheritance;
namespace ForkForward.Mapper.UnitTests;
/// <summary>
/// The map of an included member has included members of its own that it inherits through IncludeBase. In 14.0.0 the configuration was valid
/// and mapping failed, in every registration order: "variable 'Most' of type 'Most' referenced from scope '', but it is not defined".
/// </summary>
public class IncludeMembersInheritedByTheIncludedMap : InEveryRegistrationOrder
{
    public class Source
    {
        public string Name { get; set; }
        public Inner Inner { get; set; }
    }
    public class InnerBase
    {
        public string Description { get; set; }
        public Most Most { get; set; }
    }
    public class Inner : InnerBase
    {
        public int Extra { get; set; }
    }
    public class Most
    {
        public string Title { get; set; }
        public Deep Deep { get; set; }
    }
    public class Deep
    {
        public string Code { get; set; }
    }
    public class Destination
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Title { get; set; }
        public string Code { get; set; }
        public string FromAfterMap { get; set; }
    }
    public class GenericInnerBase<T>
    {
        public string Description { get; set; }
        public T Most { get; set; }
    }
    public class DerivedInner : GenericInnerBase<Most>
    {
        public int Extra { get; set; }
    }
    public class SourceWithDerivedInner
    {
        public string Name { get; set; }
        public DerivedInner Inner { get; set; }
    }
    public class Other
    {
        public string Code { get; set; }
    }
    public class SourceWithOther
    {
        public string Name { get; set; }
        public string Title { get; set; }
        public Inner Inner { get; set; }
        public Other Other { get; set; }
    }
    static void SourceMap(IProfileExpression c) => c.CreateMap<Source, Destination>().IncludeMembers(s => s.Inner)
        .ForMember(d => d.Code, o => o.Ignore()).ForMember(d => d.FromAfterMap, o => o.Ignore());
    static void InnerMap(IProfileExpression c) => c.CreateMap<Inner, Destination>(MemberList.None).IncludeBase<InnerBase, Destination>();
    static void InnerBaseMap(IProfileExpression c) => c.CreateMap<InnerBase, Destination>(MemberList.None).IncludeMembers(s => s.Most);
    static void MostMap(IProfileExpression c) => c.CreateMap<Most, Destination>(MemberList.None);
    static Source NewSource() => new() { Name = "name", Inner = new() { Description = "description", Most = new() { Title = "title", Deep = new() { Code = "code" } } } };
    [Fact]
    public void Should_map_the_members_of_the_inherited_included_member() => InEveryOrder([SourceMap, InnerMap, InnerBaseMap, MostMap], mapper =>
    {
        var destination = mapper.Map<Destination>(NewSource());
        destination.Name.ShouldBe("name");
        destination.Description.ShouldBe("description");
        destination.Title.ShouldBe("title");
        var fromInner = mapper.Map<Destination>(NewSource().Inner);
        fromInner.Name.ShouldBeNull();
        fromInner.Description.ShouldBe("description");
        fromInner.Title.ShouldBe("title");
        var withoutInner = mapper.Map<Destination>(new Source { Name = "name" });
        withoutInner.Name.ShouldBe("name");
        withoutInner.Description.ShouldBeNull();
        withoutInner.Title.ShouldBeNull();
        var withoutMost = mapper.Map<Destination>(new Source { Name = "name", Inner = new() { Description = "description" } });
        withoutMost.Description.ShouldBe("description");
        withoutMost.Title.ShouldBeNull();
        var projected = mapper.ProjectTo<Destination>(new[] { NewSource() }.AsQueryable()).Single();
        projected.Name.ShouldBe("name");
        projected.Description.ShouldBe("description");
        projected.Title.ShouldBe("title");
    });
    [Fact]
    public void The_inherited_included_member_can_include_members_and_have_an_after_map() => InEveryOrder([
        c => c.CreateMap<Source, Destination>().IncludeMembers(s => s.Inner).ForMember(d => d.FromAfterMap, o => o.Ignore()),
        InnerMap,
        InnerBaseMap,
        c => c.CreateMap<Most, Destination>(MemberList.None).IncludeMembers(s => s.Deep).AfterMap((source, destination) => destination.FromAfterMap = source?.Title),
        c => c.CreateMap<Deep, Destination>(MemberList.None)], mapper =>
    {
        var destination = mapper.Map<Destination>(NewSource());
        destination.Name.ShouldBe("name");
        destination.Description.ShouldBe("description");
        destination.Title.ShouldBe("title");
        destination.Code.ShouldBe("code");
        destination.FromAfterMap.ShouldBe("title");
        var withoutDeep = mapper.Map<Destination>(new Source { Name = "name", Inner = new() { Most = new() { Title = "title" } } });
        withoutDeep.Title.ShouldBe("title");
        withoutDeep.Code.ShouldBeNull();
        withoutDeep.FromAfterMap.ShouldBe("title");
        var withoutInner = mapper.Map<Destination>(new Source { Name = "name" });
        withoutInner.Title.ShouldBeNull();
        withoutInner.Code.ShouldBeNull();
        withoutInner.FromAfterMap.ShouldBeNull();
    });
    // The included map is not generic, the map it inherits the included members from is a closed generic map. In 14.0.0 the configuration failed
    // with a NullReferenceException in half of the orders, and mapping failed in the other half.
    [Fact]
    public void The_included_map_can_inherit_them_from_a_closed_generic_map() => InEveryOrder([
        c => c.CreateMap<SourceWithDerivedInner, Destination>().IncludeMembers(s => s.Inner)
            .ForMember(d => d.Code, o => o.Ignore()).ForMember(d => d.FromAfterMap, o => o.Ignore()),
        c => c.CreateMap<DerivedInner, Destination>(MemberList.None).IncludeBase<GenericInnerBase<Most>, Destination>(),
        c => c.CreateMap(typeof(GenericInnerBase<>), typeof(Destination), MemberList.None).IncludeMembers("Most"),
        MostMap], mapper =>
    {
        var destination = mapper.Map<Destination>(new SourceWithDerivedInner { Name = "name", Inner = new() { Description = "description", Most = new() { Title = "title" } } });
        destination.Name.ShouldBe("name");
        destination.Description.ShouldBe("description");
        destination.Title.ShouldBe("title");
        mapper.Map<Destination>(new SourceWithDerivedInner { Name = "name" }).Title.ShouldBeNull();
    });
    // Unchanged from 14.0.0, where this worked because nothing came from the inherited included member: the map takes Title from its own source,
    // and Code, which the included map ignores, from the member it includes next.
    [Fact]
    public void A_member_the_included_map_ignores_should_come_from_the_next_included_member() => InEveryOrder([
        c => c.CreateMap<SourceWithOther, Destination>().IncludeMembers(s => s.Inner, s => s.Other).ForMember(d => d.FromAfterMap, o => o.Ignore()),
        c => c.CreateMap<Inner, Destination>(MemberList.None).IncludeBase<InnerBase, Destination>().ForMember(d => d.Code, o => o.Ignore()),
        InnerBaseMap,
        c => c.CreateMap<Most, Destination>(MemberList.None).ForMember(d => d.Code, o => o.MapFrom(s => s.Deep.Code)),
        c => c.CreateMap<Other, Destination>(MemberList.None)], mapper =>
    {
        var destination = mapper.Map<Destination>(new SourceWithOther
        {
            Name = "name", Title = "own title", Other = new() { Code = "other code" },
            Inner = new() { Description = "description", Most = new() { Title = "title", Deep = new() { Code = "code" } } }
        });
        destination.Name.ShouldBe("name");
        destination.Description.ShouldBe("description");
        destination.Title.ShouldBe("own title");
        destination.Code.ShouldBe("other code");
    });
}
