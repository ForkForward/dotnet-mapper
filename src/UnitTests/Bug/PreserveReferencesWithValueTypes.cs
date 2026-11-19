namespace ForkForward.Mapper.UnitTests.Bug;

// PreserveReferences keeps the destination object of every source object in a cache, by reference, so that a source
// object that is reached twice is mapped once. The plan passed the source and the destination to that cache as they are,
// and for a value type the configuration could not be created: "Expression of type 'Source' cannot be used for parameter
// of type 'System.Object' of method 'Void CacheDestination(System.Object, System.Type, System.Object)'".
// A value type has no reference to be found by: boxed, every use of it is another object, and the cache could never
// answer. A destination that is a value type is copied wherever it is used, so there is no reference to preserve either.
// A map with a value type on either side does not use the cache: it is mapped as without PreserveReferences.
public class PreserveReferencesWithValueTypes
{
    public struct StructSource
    {
        public int Value { get; set; }
    }
    public class ClassSource
    {
        public int Value { get; set; }
    }
    public struct StructDestination
    {
        public int Value { get; set; }
    }
    public class ClassDestination
    {
        public int Value { get; set; }
    }
    // The methods of ResolutionContext a plan calls to preserve references. They are internal.
    const string CacheDestination = "CacheDestination";
    const string GetDestination = "GetDestination";
    static readonly string[] CacheMethods = [CacheDestination, GetDestination, "CheckContext"];
    sealed class MethodCalls : ExpressionVisitor
    {
        public HashSet<string> Names { get; } = [];
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            Names.Add(node.Method.Name);
            return base.VisitMethodCall(node);
        }
    }
    static MapperConfiguration Preserving<TSource, TDestination>(Action<IMappingExpression<TSource, TDestination>> configure = null)
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            var map = cfg.CreateMap<TSource, TDestination>().PreserveReferences();
            configure?.Invoke(map);
        });
        configuration.AssertConfigurationIsValid();
        return configuration;
    }
    static void ShouldNotUseTheCache<TSource, TDestination>(MapperConfiguration configuration)
    {
        configuration.FindTypeMapFor<TSource, TDestination>().PreserveReferences.ShouldBeFalse();
        var calls = new MethodCalls();
        calls.Visit(configuration.BuildExecutionPlan(typeof(TSource), typeof(TDestination)));
        calls.Names.Intersect(CacheMethods).ShouldBeEmpty();
    }

    [Fact]
    public void A_struct_source_with_a_class_destination()
    {
        var configuration = Preserving<StructSource, ClassDestination>();
        ShouldNotUseTheCache<StructSource, ClassDestination>(configuration);
        var mapper = configuration.CreateMapper();

        mapper.Map<ClassDestination>(new StructSource { Value = 3 }).Value.ShouldBe(3);
        mapper.Map(new StructSource { Value = 3 }, new ClassDestination { Value = 1 }).Value.ShouldBe(3);
        // The same value twice is two destination objects, as without PreserveReferences.
        var source = new StructSource { Value = 5 };
        var destinations = mapper.Map<List<ClassDestination>>(new[] { source, source });
        destinations.Select(destination => destination.Value).ShouldBe([5, 5]);
        destinations[0].ShouldNotBeSameAs(destinations[1]);
    }
    [Fact]
    public void A_class_source_with_a_struct_destination()
    {
        var configuration = Preserving<ClassSource, StructDestination>();
        ShouldNotUseTheCache<ClassSource, StructDestination>(configuration);
        var mapper = configuration.CreateMapper();

        mapper.Map<StructDestination>(new ClassSource { Value = 3 }).Value.ShouldBe(3);
        var source = new ClassSource { Value = 5 };
        mapper.Map<StructDestination[]>(new[] { source, source }).Select(destination => destination.Value).ShouldBe([5, 5]);
    }
    [Fact]
    public void A_struct_source_with_a_struct_destination()
    {
        var configuration = Preserving<StructSource, StructDestination>();
        ShouldNotUseTheCache<StructSource, StructDestination>(configuration);
        var mapper = configuration.CreateMapper();

        mapper.Map<StructDestination>(new StructSource { Value = 3 }).Value.ShouldBe(3);
        var source = new StructSource { Value = 5 };
        mapper.Map<StructDestination[]>(new[] { source, source }).Select(destination => destination.Value).ShouldBe([5, 5]);
    }
    [Fact]
    public void A_nullable_struct_source()
    {
        // The members of a StructSource? are HasValue and Value, so the member is configured.
        var configuration = Preserving<StructSource?, ClassDestination>(map => map.ForMember(d => d.Value, o => o.MapFrom(s => s.Value.Value)));
        ShouldNotUseTheCache<StructSource?, ClassDestination>(configuration);
        var mapper = configuration.CreateMapper();

        mapper.Map<StructSource?, ClassDestination>(new StructSource { Value = 3 }).Value.ShouldBe(3);
        mapper.Map<StructSource?, ClassDestination>(null).ShouldBeNull();
    }
    [Fact]
    public void A_nullable_struct_destination()
    {
        var configuration = Preserving<ClassSource, StructDestination?>(map => map.ConstructUsing(s => new StructDestination { Value = s.Value }));
        ShouldNotUseTheCache<ClassSource, StructDestination?>(configuration);
        var mapper = configuration.CreateMapper();

        mapper.Map<ClassSource, StructDestination?>(new ClassSource { Value = 3 }).Value.Value.ShouldBe(3);
        mapper.Map<ClassSource, StructDestination?>(null).ShouldBeNull();
    }
    [Fact]
    public void The_non_generic_CreateMap()
    {
        var configuration = new MapperConfiguration(cfg => cfg.CreateMap(typeof(StructSource), typeof(ClassDestination)).PreserveReferences());
        configuration.AssertConfigurationIsValid();
        ShouldNotUseTheCache<StructSource, ClassDestination>(configuration);

        configuration.CreateMapper().Map<ClassDestination>(new StructSource { Value = 3 }).Value.ShouldBe(3);
    }
    public struct Wrapper<T>
    {
        public T Value { get; set; }
    }
    public class WrapperDto<T>
    {
        public T Value { get; set; }
    }
    [Fact]
    public void An_open_generic_map_closed_with_a_struct()
    {
        var configuration = new MapperConfiguration(cfg => cfg.CreateMap(typeof(Wrapper<>), typeof(WrapperDto<>)).PreserveReferences());
        configuration.AssertConfigurationIsValid();

        configuration.CreateMapper().Map<WrapperDto<int>>(new Wrapper<int> { Value = 3 }).Value.ShouldBe(3);
    }
    [Fact]
    public void The_plan_is_the_one_without_PreserveReferences()
    {
        var with = new MapperConfiguration(cfg => cfg.CreateMap<StructSource, ClassDestination>().PreserveReferences());
        var without = new MapperConfiguration(cfg => cfg.CreateMap<StructSource, ClassDestination>());

        MethodNames(with).ShouldBe(MethodNames(without));
        static string[] MethodNames(MapperConfiguration configuration)
        {
            var calls = new MethodCalls();
            calls.Visit(configuration.BuildExecutionPlan(typeof(StructSource), typeof(ClassDestination)));
            return [.. calls.Names.Order()];
        }
    }
    [Fact]
    public void A_map_of_classes_still_uses_the_cache()
    {
        var configuration = Preserving<ClassSource, ClassDestination>();
        configuration.FindTypeMapFor<ClassSource, ClassDestination>().PreserveReferences.ShouldBeTrue();
        var calls = new MethodCalls();
        calls.Visit(configuration.BuildExecutionPlan(typeof(ClassSource), typeof(ClassDestination)));
        calls.Names.ShouldContain(CacheDestination);
        calls.Names.ShouldContain(GetDestination);

        var source = new ClassSource { Value = 5 };
        var destinations = configuration.CreateMapper().Map<List<ClassDestination>>(new[] { source, source });
        destinations[0].ShouldBeSameAs(destinations[1]);
    }

    // A struct inside a graph of classes: the classes preserve references, the struct is mapped wherever it is reached.
    public struct Point
    {
        public int X { get; set; }
        public int Y { get; set; }
    }
    public class PointDto
    {
        public int X { get; set; }
        public int Y { get; set; }
    }
    public class Parent
    {
        public Point Location { get; set; }
        public Tag Tag { get; set; }
        public List<Child> Children { get; set; } = [];
    }
    public class Child
    {
        public Point Location { get; set; }
        public Tag Tag { get; set; }
        public Parent Parent { get; set; }
    }
    public class Tag
    {
        public string Name { get; set; }
    }
    public class ParentDto
    {
        public PointDto Location { get; set; }
        public TagDto Tag { get; set; }
        public List<ChildDto> Children { get; set; }
    }
    public class ChildDto
    {
        public PointDto Location { get; set; }
        public TagDto Tag { get; set; }
        public ParentDto Parent { get; set; }
    }
    public class TagDto
    {
        public string Name { get; set; }
    }
    [Fact]
    public void A_struct_inside_a_graph_of_classes_that_preserves_references()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Parent, ParentDto>().PreserveReferences();
            cfg.CreateMap<Child, ChildDto>().PreserveReferences();
            cfg.CreateMap<Tag, TagDto>().PreserveReferences();
            cfg.CreateMap<Point, PointDto>().PreserveReferences();
        });
        configuration.AssertConfigurationIsValid();
        configuration.FindTypeMapFor<Parent, ParentDto>().PreserveReferences.ShouldBeTrue();
        configuration.FindTypeMapFor<Child, ChildDto>().PreserveReferences.ShouldBeTrue();
        configuration.FindTypeMapFor<Tag, TagDto>().PreserveReferences.ShouldBeTrue();
        configuration.FindTypeMapFor<Point, PointDto>().PreserveReferences.ShouldBeFalse();
        var tag = new Tag { Name = "shared" };
        var location = new Point { X = 1, Y = 2 };
        var parent = new Parent { Location = location, Tag = tag };
        parent.Children.Add(new Child { Location = location, Tag = tag, Parent = parent });
        parent.Children.Add(new Child { Location = new Point { X = 3, Y = 4 }, Tag = tag, Parent = parent });

        var destination = configuration.CreateMapper().Map<ParentDto>(parent);

        destination.Children.Count.ShouldBe(2);
        destination.Children[0].Parent.ShouldBeSameAs(destination);
        destination.Children[1].Parent.ShouldBeSameAs(destination);
        destination.Tag.Name.ShouldBe("shared");
        destination.Children[0].Tag.ShouldBeSameAs(destination.Tag);
        destination.Children[1].Tag.ShouldBeSameAs(destination.Tag);
        (destination.Location.X, destination.Location.Y).ShouldBe((1, 2));
        (destination.Children[0].Location.X, destination.Children[0].Location.Y).ShouldBe((1, 2));
        (destination.Children[1].Location.X, destination.Children[1].Location.Y).ShouldBe((3, 4));
        destination.Children[0].Location.ShouldNotBeSameAs(destination.Location);
    }

    // A struct that holds the way back: only its map was asked to preserve references. That has no effect, and the
    // cycle is found as if nothing had been asked: the map of the class gets reference preservation by itself.
    public class Owner
    {
        public int Id { get; set; }
        public Link Link { get; set; }
    }
    public struct Link
    {
        public int Id { get; set; }
        public Owner Owner { get; set; }
    }
    public class OwnerDto
    {
        public int Id { get; set; }
        public LinkDto Link { get; set; }
    }
    public class LinkDto
    {
        public int Id { get; set; }
        public OwnerDto Owner { get; set; }
    }
    [Fact]
    public void The_classes_around_a_struct_that_was_asked_to_preserve_references_still_do()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Link, LinkDto>().PreserveReferences();
            cfg.CreateMap<Owner, OwnerDto>();
        });
        configuration.AssertConfigurationIsValid();
        configuration.FindTypeMapFor<Link, LinkDto>().PreserveReferences.ShouldBeFalse();
        configuration.FindTypeMapFor<Owner, OwnerDto>().PreserveReferences.ShouldBeTrue();
        var owner = new Owner { Id = 1 };
        owner.Link = new Link { Id = 2, Owner = owner };

        var destination = configuration.CreateMapper().Map<OwnerDto>(owner);

        destination.Id.ShouldBe(1);
        destination.Link.Id.ShouldBe(2);
        destination.Link.Owner.ShouldBeSameAs(destination);
    }

    // Reference preservation that is set automatically reaches a struct through the derived maps of a map in a cycle.
    public interface INode
    {
        int Id { get; }
        Root Root { get; }
    }
    public class Root
    {
        public int Id { get; set; }
        public INode First { get; set; }
        public INode Second { get; set; }
    }
    public struct StructNode : INode
    {
        public int Id { get; set; }
        public Root Root { get; set; }
    }
    public class ClassNode : INode
    {
        public int Id { get; set; }
        public Root Root { get; set; }
    }
    public class RootDto
    {
        public int Id { get; set; }
        public NodeDto First { get; set; }
        public NodeDto Second { get; set; }
    }
    public class NodeDto
    {
        public int Id { get; set; }
        public RootDto Root { get; set; }
    }
    [Theory, InlineData(true), InlineData(false)]
    public void A_struct_in_a_cycle_of_types_found_by_the_configuration(bool nodesFirst)
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            if (!nodesFirst)
            {
                cfg.CreateMap<Root, RootDto>();
            }
            cfg.CreateMap<INode, NodeDto>().Include<StructNode, NodeDto>().Include<ClassNode, NodeDto>();
            cfg.CreateMap<StructNode, NodeDto>();
            cfg.CreateMap<ClassNode, NodeDto>();
            if (nodesFirst)
            {
                cfg.CreateMap<Root, RootDto>();
            }
        });
        configuration.AssertConfigurationIsValid();
        configuration.FindTypeMapFor<Root, RootDto>().PreserveReferences.ShouldBeTrue();
        configuration.FindTypeMapFor<INode, NodeDto>().PreserveReferences.ShouldBeTrue();
        configuration.FindTypeMapFor<ClassNode, NodeDto>().PreserveReferences.ShouldBeTrue();
        configuration.FindTypeMapFor<StructNode, NodeDto>().PreserveReferences.ShouldBeFalse();
        var mapper = configuration.CreateMapper();

        var root = new Root { Id = 1 };
        root.First = new StructNode { Id = 2, Root = root };
        root.Second = root.First;
        var destination = mapper.Map<RootDto>(root);
        destination.Id.ShouldBe(1);
        destination.First.Id.ShouldBe(2);
        destination.First.Root.ShouldBeSameAs(destination);
        destination.Second.Id.ShouldBe(2);
        destination.Second.Root.ShouldBeSameAs(destination);
        destination.Second.ShouldNotBeSameAs(destination.First);

        root = new Root { Id = 1 };
        root.First = new ClassNode { Id = 2, Root = root };
        root.Second = root.First;
        destination = mapper.Map<RootDto>(root);
        destination.First.Id.ShouldBe(2);
        destination.First.Root.ShouldBeSameAs(destination);
        destination.Second.ShouldBeSameAs(destination.First);
    }
}
