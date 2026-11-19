using System.Collections.Immutable;
namespace ForkForward.Mapper.UnitTests.Projection;

// How ProjectTo creates a destination collection: the source as it is where it can be assigned, ToArray for an
// array, ToList where a List<T> can be assigned, otherwise a public constructor of the destination type that takes
// the source collection. A set interface, ISet<T> or IReadOnlySet<T>, fits none of these, and the source collection
// was handed on as it was: "Argument types do not match" from the expression tree library. Now a set interface
// gets a HashSet<T>, as it does in Map, and a collection that cannot be created is refused with a message that
// says so.
public class ProjectionDestinationCollections
{
    public class Holder<T>
    {
        public T Value { get; set; }
    }
    public class Item
    {
        public int Id { get; set; }
    }
    public class ItemDto
    {
        public int Id { get; set; }
    }
    static readonly int[] Numbers = [1, 2, 2, 3];
    static readonly Item[] Items = [new() { Id = 1 }, new() { Id = 2 }];

    static IQueryable<Holder<TDestination>> Project<TSource, TDestination>(TSource source)
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.CreateProjection<Holder<TSource>, Holder<TDestination>>();
            cfg.CreateProjection<Item, ItemDto>();
        });
        return new[] { new Holder<TSource> { Value = source } }.AsQueryable().ProjectTo<Holder<TDestination>>(configuration);
    }
    // The expression the member is bound to in the projection.
    static Expression Member(IQueryable query)
    {
        var select = (MethodCallExpression)query.Expression;
        var projection = (LambdaExpression)((UnaryExpression)select.Arguments[1]).Operand;
        return ((MemberAssignment)((MemberInitExpression)projection.Body).Bindings.Single()).Expression;
    }
    static void ShouldCreateAHashSet<TSource, TDestination>(TSource source) where TDestination : IEnumerable<int>
    {
        // Before: an ArgumentException, "Argument types do not match", inside "Error building the projection.".
        var query = Project<TSource, TDestination>(source);

        // new HashSet<int>(source.Value), which is what a HashSet<int> destination gets as well.
        var created = Member(query).ShouldBeAssignableTo<NewExpression>();
        created.Type.ShouldBe(typeof(HashSet<int>));
        created.Arguments.ShouldHaveSingleItem().ShouldBeAssignableTo<MemberExpression>().Member.Name.ShouldBe(nameof(Holder<TSource>.Value));
        query.Single().Value.ShouldBeOfType<HashSet<int>>().ShouldBe(new[] { 1, 2, 3 }, ignoreOrder: true);
    }

    [Fact]
    public void A_list_is_projected_into_ISet() => ShouldCreateAHashSet<List<int>, ISet<int>>([.. Numbers]);
    [Fact]
    public void A_list_is_projected_into_IReadOnlySet() => ShouldCreateAHashSet<List<int>, IReadOnlySet<int>>([.. Numbers]);
    [Fact]
    public void An_array_is_projected_into_ISet() => ShouldCreateAHashSet<int[], ISet<int>>(Numbers);
    [Fact]
    public void An_IEnumerable_is_projected_into_ISet() => ShouldCreateAHashSet<IEnumerable<int>, ISet<int>>(Numbers);
    [Fact]
    public void An_ICollection_is_projected_into_IReadOnlySet() => ShouldCreateAHashSet<ICollection<int>, IReadOnlySet<int>>(Numbers);

    [Fact]
    public void A_list_of_mapped_elements_is_projected_into_ISet()
    {
        // Before: the same ArgumentException.
        var query = Project<List<Item>, ISet<ItemDto>>([.. Items]);

        // new HashSet<ItemDto>(source.Value.Select(item => new ItemDto { Id = item.Id }))
        var created = Member(query).ShouldBeAssignableTo<NewExpression>();
        created.Type.ShouldBe(typeof(HashSet<ItemDto>));
        created.Arguments.ShouldHaveSingleItem().ShouldBeAssignableTo<MethodCallExpression>().Method.Name.ShouldBe(nameof(Enumerable.Select));
        query.Single().Value.ShouldBeOfType<HashSet<ItemDto>>().Select(item => item.Id).ShouldBe(new[] { 1, 2 }, ignoreOrder: true);
    }

    [Fact]
    public void A_list_of_mapped_elements_is_projected_into_IReadOnlySet() =>
        Project<List<Item>, IReadOnlySet<ItemDto>>([.. Items]).Single().Value.ShouldBeOfType<HashSet<ItemDto>>().Select(item => item.Id).ShouldBe(new[] { 1, 2 }, ignoreOrder: true);

    public class SetThroughConstructor(ISet<int> value)
    {
        public ISet<int> Value { get; } = value;
    }

    [Fact]
    public void A_list_is_projected_into_a_constructor_parameter_that_is_an_ISet()
    {
        var configuration = new MapperConfiguration(cfg => cfg.CreateProjection<Holder<List<int>>, SetThroughConstructor>());
        var holders = new[] { new Holder<List<int>> { Value = [.. Numbers] } }.AsQueryable();

        // Before: an ArgumentException that names the two types and the position of the parameter, inside "Error building the projection.".
        var projected = holders.ProjectTo<SetThroughConstructor>(configuration).Single();

        projected.Value.ShouldBeOfType<HashSet<int>>().ShouldBe(new[] { 1, 2, 3 }, ignoreOrder: true);
    }

    [Fact]
    public void A_set_is_handed_on_as_it_is_where_it_can_be_assigned()
    {
        // This worked before, and gives the same: no copy is made.
        HashSet<int> source = [.. Numbers];

        var query = Project<HashSet<int>, ISet<int>>(source);

        Member(query).NodeType.ShouldBe(ExpressionType.Convert);
        query.Single().Value.ShouldBeSameAs(source);
    }

    // What the destination types that could be created before are given, from a List<int>. None of it has changed.
    [Fact]
    public void A_list_is_handed_on_as_it_is_where_it_can_be_assigned()
    {
        List<int> source = [.. Numbers];

        ShouldBeTheSource(Project<List<int>, IReadOnlyList<int>>(source));
        ShouldBeTheSource(Project<List<int>, IReadOnlyCollection<int>>(source));
        ShouldBeTheSource(Project<List<int>, ICollection<int>>(source));
        ShouldBeTheSource(Project<List<int>, IList<int>>(source));
        ShouldBeTheSource(Project<List<int>, IEnumerable<int>>(source));
        Member(Project<List<int>, List<int>>(source)).NodeType.ShouldBe(ExpressionType.MemberAccess);
        Project<List<int>, List<int>>(source).Single().Value.ShouldBeSameAs(source);

        void ShouldBeTheSource<TDestination>(IQueryable<Holder<TDestination>> query)
        {
            Member(query).NodeType.ShouldBe(ExpressionType.Convert);
            query.Single().Value.ShouldBeSameAs(source);
        }
    }

    [Fact]
    public void A_list_is_copied_into_an_array_with_ToArray()
    {
        var query = Project<List<int>, int[]>([.. Numbers]);

        Member(query).ShouldBeAssignableTo<MethodCallExpression>().Method.Name.ShouldBe(nameof(Enumerable.ToArray));
        query.Single().Value.ShouldBe(Numbers);
    }

    [Fact]
    public void An_array_is_copied_into_a_list_with_ToList()
    {
        var query = Project<int[], List<int>>(Numbers);

        Member(query).ShouldBeAssignableTo<MethodCallExpression>().Method.Name.ShouldBe(nameof(Enumerable.ToList));
        query.Single().Value.ShouldBe(Numbers);
    }

    [Fact]
    public void A_list_is_passed_to_the_constructor_of_a_destination_type_that_takes_it()
    {
        List<int> source = [.. Numbers];

        ShouldBeConstructed(Project<List<int>, HashSet<int>>(source)).ShouldBe(new[] { 1, 2, 3 }, ignoreOrder: true);
        ShouldBeConstructed(Project<List<int>, SortedSet<int>>(source)).ShouldBe(new[] { 1, 2, 3 });
        ShouldBeConstructed(Project<List<int>, Queue<int>>(source)).ShouldBe(Numbers);
        ShouldBeConstructed(Project<List<int>, Collection<int>>(source)).ShouldBe(Numbers);
        ShouldBeConstructed(Project<List<int>, ReadOnlyCollection<int>>(source)).ShouldBe(Numbers);

        static TDestination ShouldBeConstructed<TDestination>(IQueryable<Holder<TDestination>> query)
        {
            var created = Member(query).ShouldBeAssignableTo<NewExpression>();
            created.Type.ShouldBe(typeof(TDestination));
            created.Arguments.ShouldHaveSingleItem().ShouldBeAssignableTo<MemberExpression>();
            return query.Single().Value.ShouldBeOfType<TDestination>();
        }
    }

    [Fact]
    public void Mapped_elements_are_copied_into_what_the_destination_type_asks_for()
    {
        List<Item> source = [.. Items];

        Project<List<Item>, HashSet<ItemDto>>(source).Single().Value.Select(item => item.Id).ShouldBe(new[] { 1, 2 }, ignoreOrder: true);
        Project<List<Item>, IReadOnlyList<ItemDto>>(source).Single().Value.ShouldBeOfType<List<ItemDto>>().Select(item => item.Id).ShouldBe(new[] { 1, 2 });
        Project<List<Item>, IReadOnlyCollection<ItemDto>>(source).Single().Value.ShouldBeOfType<List<ItemDto>>().Count.ShouldBe(2);
        Project<List<Item>, ICollection<ItemDto>>(source).Single().Value.ShouldBeOfType<List<ItemDto>>().Count.ShouldBe(2);
        Project<List<Item>, ItemDto[]>(source).Single().Value.Select(item => item.Id).ShouldBe(new[] { 1, 2 });
        Project<List<Item>, IEnumerable<ItemDto>>(source).Single().Value.Select(item => item.Id).ShouldBe(new[] { 1, 2 });
    }

    static void ShouldSayWhatCannotBeCreated<TSource, TDestination>(TSource source, Type constructorArgument)
    {
        // Before: an ArgumentException, "Argument types do not match", inside "Error building the projection.".
        var exception = Should.Throw<AutoMapperMappingException>(() => Project<TSource, TDestination>(source));

        exception.InnerException.ShouldBeNull();
        exception.Message.ShouldStartWith($"ProjectTo cannot create {typeof(TDestination)} from {typeof(TSource)}:");
        Assert.Contains($"no public constructor that takes {constructorArgument}.", exception.Message);
        exception.MemberMap.DestinationName.ShouldBe(nameof(Holder<TDestination>.Value));
        exception.TypeMap.Types.ShouldBe(new TypePair(typeof(Holder<TSource>), typeof(Holder<TDestination>)));
    }

    [Fact]
    public void A_collection_that_cannot_be_created_is_refused_with_both_types_and_the_member()
    {
        ShouldSayWhatCannotBeCreated<List<int>, IImmutableList<int>>([.. Numbers], typeof(List<int>));
        ShouldSayWhatCannotBeCreated<List<int>, ImmutableList<int>>([.. Numbers], typeof(List<int>));
        ShouldSayWhatCannotBeCreated<int[], ImmutableArray<int>>(Numbers, typeof(int[]));
        // The elements are mapped first, and the constructor of ReadOnlyCollection<T> takes an IList<T>.
        ShouldSayWhatCannotBeCreated<List<Item>, ReadOnlyCollection<ItemDto>>([.. Items], typeof(IEnumerable<ItemDto>));
    }

    public class ImmutableThroughConstructor(IImmutableList<int> value)
    {
        public IImmutableList<int> Value { get; } = value;
    }

    [Fact]
    public void A_constructor_parameter_that_cannot_be_created_is_refused_in_the_same_way()
    {
        var configuration = new MapperConfiguration(cfg => cfg.CreateProjection<Holder<List<int>>, ImmutableThroughConstructor>());
        var holders = new[] { new Holder<List<int>> { Value = [.. Numbers] } }.AsQueryable();

        var exception = Should.Throw<AutoMapperMappingException>(() => holders.ProjectTo<ImmutableThroughConstructor>(configuration));

        exception.Message.ShouldStartWith($"ProjectTo cannot create {typeof(IImmutableList<int>)} from {typeof(List<int>)}:");
        exception.MemberMap.ShouldBeOfType<ConstructorParameterMap>().DestinationName.ShouldBe("value");
    }
}
