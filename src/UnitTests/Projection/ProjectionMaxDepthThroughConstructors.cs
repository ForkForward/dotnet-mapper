namespace ForkForward.Mapper.UnitTests.Projection;

// In ProjectTo a map that reached its MaxDepth gives null. When AllowNullDestinationValues is off it gives an empty
// destination instead: one that is created, with none of its members projected. The parameters of its constructor
// were projected all the same, so a model that continues through a constructor parameter went on past the limit,
// as far as RecursiveQueriesMaxDepth allows. Now the constructor gets the default values of its parameters there,
// and such a model stops at the same depth as one that continues through a property.
public class ProjectionMaxDepthThroughConstructors
{
    const int RecursiveQueriesMaxDepth = 20;
    public class Node
    {
        public int Id { get; set; }
        public Node Next { get; set; }
    }
    public class PropertyDto
    {
        public int Id { get; set; }
        public PropertyDto Next { get; set; }
    }
    public class ConstructorDto
    {
        public ConstructorDto(int id, ConstructorDto next)
        {
            Id = id;
            Next = next;
        }
        public int Id { get; }
        public ConstructorDto Next { get; }
    }
    public record RecordDto(int Id, RecordDto Next);
    // The next level through the constructor, the value through a property.
    public class MixedDto
    {
        public MixedDto(MixedDto next) => Next = next;
        public int Id { get; set; }
        public MixedDto Next { get; }
    }

    static MapperConfiguration Maps(int maxDepth, bool allowNull) => new(cfg =>
    {
        cfg.Internal().RecursiveQueriesMaxDepth = RecursiveQueriesMaxDepth;
        cfg.AllowNullDestinationValues = allowNull;
        cfg.CreateProjection<Node, PropertyDto>().MaxDepth(maxDepth);
        cfg.CreateProjection<Node, ConstructorDto>().MaxDepth(maxDepth);
        cfg.CreateProjection<Node, RecordDto>().MaxDepth(maxDepth);
        cfg.CreateProjection<Node, MixedDto>().MaxDepth(maxDepth);
    });
    // A chain longer than RecursiveQueriesMaxDepth, with the ids 1, 2, 3...
    static IQueryable<Node> Chain()
    {
        Node node = null;
        for(var id = RecursiveQueriesMaxDepth + 10; id >= 1; id--)
        {
            node = new Node { Id = id, Next = node };
        }
        return new[] { node }.AsQueryable();
    }
    static List<int> Ids<T>(T first, Func<T, T> next, Func<T, int> id) where T : class
    {
        List<int> ids = [];
        for(var item = first; item != null; item = next(item))
        {
            ids.Add(id(item));
        }
        return ids;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void A_chain_through_a_constructor_parameter_ends_where_one_through_a_property_ends_when_null_destination_values_are_not_allowed(int maxDepth)
    {
        var configuration = Maps(maxDepth, allowNull: false);

        var throughProperty = Ids(Chain().ProjectTo<PropertyDto>(configuration).Single(), d => d.Next, d => d.Id);
        var throughConstructor = Ids(Chain().ProjectTo<ConstructorDto>(configuration).Single(), d => d.Next, d => d.Id);
        var throughRecord = Ids(Chain().ProjectTo<RecordDto>(configuration).Single(), d => d.Next, d => d.Id);
        var throughBoth = Ids(Chain().ProjectTo<MixedDto>(configuration).Single(), d => d.Next, d => d.Id);

        // The levels the limit allows, and the empty destination that stands for the next one. This is what the
        // chain through a property gave before as well.
        throughProperty.ShouldBe(Enumerable.Range(1, maxDepth).Append(0));
        // Before: 21 levels each, with the ids 1 to 21 for the first two. In the third, only the levels the limit
        // allows had their id, the property, and the 18 after them were empty but for the next level.
        throughConstructor.ShouldBe(throughProperty);
        throughRecord.ShouldBe(throughProperty);
        throughBoth.ShouldBe(throughProperty);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void Both_end_with_null_at_the_limit_when_null_destination_values_are_allowed(int maxDepth)
    {
        // This worked before, and gives the same.
        var configuration = Maps(maxDepth, allowNull: true);

        var throughProperty = Ids(Chain().ProjectTo<PropertyDto>(configuration).Single(), d => d.Next, d => d.Id);
        var throughConstructor = Ids(Chain().ProjectTo<ConstructorDto>(configuration).Single(), d => d.Next, d => d.Id);
        var throughRecord = Ids(Chain().ProjectTo<RecordDto>(configuration).Single(), d => d.Next, d => d.Id);
        var throughBoth = Ids(Chain().ProjectTo<MixedDto>(configuration).Single(), d => d.Next, d => d.Id);

        throughProperty.ShouldBe(Enumerable.Range(1, maxDepth));
        throughConstructor.ShouldBe(throughProperty);
        throughRecord.ShouldBe(throughProperty);
        throughBoth.ShouldBe(throughProperty);
    }

    [Fact]
    public void Without_MaxDepth_a_chain_through_a_constructor_parameter_is_projected_as_before()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.Internal().RecursiveQueriesMaxDepth = 5;
            cfg.AllowNullDestinationValues = false;
            cfg.CreateProjection<Node, ConstructorDto>();
        });

        Ids(Chain().ProjectTo<ConstructorDto>(configuration).Single(), d => d.Next, d => d.Id).ShouldBe(Enumerable.Range(1, 6));
    }

    public class DefaultsDto
    {
        public DefaultsDto(DefaultsDto next, int id = -1, string name = "none")
        {
            Next = next;
            Id = id;
            Name = name;
        }
        public int Id { get; }
        public string Name { get; }
        public DefaultsDto Next { get; }
    }

    [Fact]
    public void The_empty_destination_is_created_with_the_default_values_of_its_constructor_parameters()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.Internal().RecursiveQueriesMaxDepth = RecursiveQueriesMaxDepth;
            cfg.AllowNullDestinationValues = false;
            cfg.CreateProjection<Node, DefaultsDto>().MaxDepth(2).ForCtorParam("name", o => o.MapFrom(s => "node " + s.Id));
        });

        var first = Chain().ProjectTo<DefaultsDto>(configuration).Single();

        (first.Id, first.Name).ShouldBe((1, "node 1"));
        (first.Next.Id, first.Next.Name).ShouldBe((2, "node 2"));
        // The default of an optional parameter, default(T) of any other.
        var empty = first.Next.Next;
        (empty.Id, empty.Name).ShouldBe((-1, "none"));
        empty.Next.ShouldBeNull();
    }

    public class Tree
    {
        public int Id { get; set; }
        public List<Tree> Children { get; set; } = [];
    }
    public class PropertyTreeDto
    {
        public int Id { get; set; }
        public List<PropertyTreeDto> Children { get; set; }
    }
    public class ConstructorTreeDto
    {
        public ConstructorTreeDto(int id, List<ConstructorTreeDto> children)
        {
            Id = id;
            Children = children;
        }
        public int Id { get; }
        public List<ConstructorTreeDto> Children { get; }
    }

    [Fact]
    public void A_tree_through_a_constructor_parameter_ends_where_one_through_a_property_ends()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.Internal().RecursiveQueriesMaxDepth = RecursiveQueriesMaxDepth;
            cfg.AllowNullDestinationValues = false;
            cfg.CreateProjection<Tree, PropertyTreeDto>().MaxDepth(2);
            cfg.CreateProjection<Tree, ConstructorTreeDto>().MaxDepth(2);
        });
        // Four levels, two children each.
        Tree Grow(int id, int levels) => new() { Id = id, Children = levels == 1 ? [] : [Grow(id * 10 + 1, levels - 1), Grow(id * 10 + 2, levels - 1)] };
        var trees = new[] { Grow(1, 4) }.AsQueryable();

        var throughProperty = trees.ProjectTo<PropertyTreeDto>(configuration).Single();
        var throughConstructor = trees.ProjectTo<ConstructorTreeDto>(configuration).Single();

        // Two levels, and an empty destination for each child of the second.
        string Describe<T>(T tree, Func<T, int> id, Func<T, List<T>> children) =>
            children(tree) == null ? id(tree).ToString() : $"{id(tree)}({string.Join(" ", children(tree).Select(child => Describe(child, id, children)))})";
        Describe(throughProperty, d => d.Id, d => d.Children).ShouldBe("1(11(0 0) 12(0 0))");
        // Before: all four levels, "1(11(111(1111() 1112()) 112(1121() 1122())) 12(121(1211() 1212()) 122(1221() 1222())))".
        Describe(throughConstructor, d => d.Id, d => d.Children).ShouldBe("1(11(0 0) 12(0 0))");
    }
}
