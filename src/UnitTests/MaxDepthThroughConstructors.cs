namespace ForkForward.Mapper.UnitTests;

// MaxDepth counts the levels of one type map that are being mapped, and maps no level beyond the limit.
// The count used to start only after the destination object had been created. A next level that is mapped while
// the destination is created, as a constructor parameter or by ConstructUsing with the context, was therefore
// never counted: MaxDepth was ignored and the whole graph was mapped. Now a level is counted before its
// destination is created, and such a graph stops at the same depth as one that continues through a property.
public class MaxDepthThroughConstructors
{
    public class Node
    {
        public int Value { get; set; }
        public Node Next { get; set; }
    }
    public class PropertyDto
    {
        public int Value { get; set; }
        public PropertyDto Next { get; set; }
    }
    public class ConstructorDto
    {
        public ConstructorDto(ConstructorDto next) => Next = next;
        public int Value { get; set; }
        public ConstructorDto Next { get; }
    }
    public record RecordDto(RecordDto Next, int Value = 0);
    public class Tree
    {
        public Tree Left { get; set; }
        public Tree Right { get; set; }
    }
    public class TreeDto
    {
        public TreeDto(TreeDto left) => Left = left;
        public TreeDto Left { get; }
        public TreeDto Right { get; set; }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(10)]
    public void A_chain_through_a_constructor_parameter_stops_at_the_same_depth_as_one_through_a_property(int maxDepth)
    {
        var mapper = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Node, PropertyDto>().MaxDepth(maxDepth);
            cfg.CreateMap<Node, ConstructorDto>().MaxDepth(maxDepth);
            cfg.CreateMap<Node, RecordDto>().MaxDepth(maxDepth);
        }).CreateMapper();
        var source = Chain(100);

        var throughProperty = Values(mapper.Map<PropertyDto>(source), d => d.Next, d => d.Value);
        var throughConstructor = Values(mapper.Map<ConstructorDto>(source), d => d.Next, d => d.Value);
        var throughRecord = Values(mapper.Map<RecordDto>(source), d => d.Next, d => d.Value);

        // Before: the property chain had maxDepth levels, the other two all 100.
        throughProperty.ShouldBe(Enumerable.Range(0, maxDepth));
        throughConstructor.ShouldBe(throughProperty);
        throughRecord.ShouldBe(throughProperty);
    }

    [Fact]
    public void Every_path_stops_at_the_limit_when_the_next_level_is_both_a_constructor_parameter_and_a_property()
    {
        var mapper = new MapperConfiguration(cfg => cfg.CreateMap<Tree, TreeDto>().MaxDepth(3)).CreateMapper();

        var destination = mapper.Map<TreeDto>(CompleteTree(6));

        // Before: only the steps through Right, the property, were counted. The paths through Left, the constructor
        // parameter, went down all 6 levels.
        Height(destination).ShouldBe(3);
        Count(destination).ShouldBe(7);
    }

    [Fact]
    public void PreserveReferences_and_MaxDepth_together_stop_a_chain_through_a_constructor_parameter()
    {
        var mapper = new MapperConfiguration(cfg => cfg.CreateMap<Node, ConstructorDto>().PreserveReferences().MaxDepth(10)).CreateMapper();

        var destination = mapper.Map<ConstructorDto>(Chain(100));

        // Before: 100 levels.
        Values(destination, d => d.Next, d => d.Value).ShouldBe(Enumerable.Range(0, 10));
    }

    [Fact]
    public void A_cycle_through_a_constructor_parameter_ends_at_the_limit()
    {
        var mapper = new MapperConfiguration(cfg => cfg.CreateMap<Node, ConstructorDto>().MaxDepth(10)).CreateMapper();
        var first = new Node { Value = 1 };
        var second = new Node { Value = 2, Next = first };
        first.Next = second;

        // A constructor parameter cannot refer back to the object that is being constructed, so PreserveReferences
        // cannot close this cycle, and only MaxDepth can end it.
        // Before: it went round until the stack ran out, and ended in the exception about a source graph nested too deeply.
        var destination = mapper.Map<ConstructorDto>(first);

        // How much of the cycle the result holds depends on where in it the limit falls; that the mapping ends does not.
        Values(destination, d => d.Next, d => d.Value).Count.ShouldBeInRange(1, 10);
    }

    [Fact]
    public void A_mapping_error_below_the_limit_does_not_change_what_the_same_mapper_maps_next()
    {
        var mapper = new MapperConfiguration(cfg =>
        {
            // The error is thrown after the destination was constructed, by a property...
            cfg.CreateMap<Node, ConstructorDto>().MaxDepth(10).ForMember(d => d.Value, o => o.MapFrom(s => FailWhenNegative(s)));
            // ...and while it is being constructed, by a constructor parameter.
            cfg.CreateMap<Node, RecordDto>().MaxDepth(10).ForCtorParam(nameof(RecordDto.Value), o => o.MapFrom(s => FailWhenNegative(s)));
        }).CreateMapper();
        var good = Chain(100);
        var failing = Chain(100);
        failing.Next.Next.Next.Value = -1;

        Values(mapper.Map<ConstructorDto>(good), d => d.Next, d => d.Value).ShouldBe(Enumerable.Range(0, 10));
        Innermost(Should.Throw<Exception>(() => mapper.Map<ConstructorDto>(failing))).Message.ShouldBe("negative");
        Values(mapper.Map<ConstructorDto>(good), d => d.Next, d => d.Value).ShouldBe(Enumerable.Range(0, 10));

        Values(mapper.Map<RecordDto>(good), d => d.Next, d => d.Value).ShouldBe(Enumerable.Range(0, 10));
        Innermost(Should.Throw<Exception>(() => mapper.Map<RecordDto>(failing))).Message.ShouldBe("negative");
        Values(mapper.Map<RecordDto>(good), d => d.Next, d => d.Value).ShouldBe(Enumerable.Range(0, 10));
    }

    [Fact]
    public void Levels_mapped_onto_an_existing_destination_and_levels_constructed_after_them_are_counted_together()
    {
        var mapper = new MapperConfiguration(cfg => cfg.CreateMap<Node, RecordDto>().MaxDepth(10)).CreateMapper();
        var third = new RecordDto(null, -3);
        var second = new RecordDto(third, -2);
        var first = new RecordDto(second, -1);

        var destination = mapper.Map(Chain(100), first);

        // The three existing objects are mapped onto, through the properties; from the fourth level on the
        // destinations are constructed. Before: the constructed levels were not counted, and the chain had all 100 levels.
        destination.ShouldBeSameAs(first);
        destination.Next.ShouldBeSameAs(second);
        destination.Next.Next.ShouldBeSameAs(third);
        Values(destination, d => d.Next, d => d.Value).ShouldBe(Enumerable.Range(0, 10));
    }

    [Fact]
    public void A_chain_through_ConstructUsing_that_maps_with_the_context_stops_at_the_limit()
    {
        var mapper = new MapperConfiguration(cfg => cfg.CreateMap<Node, ConstructorDto>().MaxDepth(10)
            .ConstructUsing((source, context) => new ConstructorDto(source.Next == null ? null : context.Mapper.Map<ConstructorDto>(source.Next)))).CreateMapper();

        var destination = mapper.Map<ConstructorDto>(Chain(100));

        // Before: 100 levels.
        Values(destination, d => d.Next, d => d.Value).ShouldBe(Enumerable.Range(0, 10));
    }

    static int FailWhenNegative(Node node) => node.Value < 0 ? throw new InvalidOperationException("negative") : node.Value;

    static Exception Innermost(Exception exception)
    {
        while (exception.InnerException != null)
        {
            exception = exception.InnerException;
        }
        return exception;
    }

    static Node Chain(int length)
    {
        Node head = null;
        for (var value = length - 1; value >= 0; value--)
        {
            head = new Node { Value = value, Next = head };
        }
        return head;
    }

    static Tree CompleteTree(int height) => height == 0 ? null : new Tree { Left = CompleteTree(height - 1), Right = CompleteTree(height - 1) };

    static int Height(TreeDto tree) => tree == null ? 0 : 1 + Math.Max(Height(tree.Left), Height(tree.Right));

    public class OrderWithLines
    {
        public List<OrderLine> Lines { get; set; }
    }
    public class OrderLine
    {
        public int Quantity { get; set; }
    }
    public class OrderLineDto
    {
        public int Quantity { get; set; }
    }
    public class LinesPropertyDto
    {
        public List<OrderLineDto> Lines { get; set; }
    }
    public record LinesRecordDto(List<OrderLineDto> Lines);

    // The collection members of the last level a map with MaxDepth maps are left empty, whatever they hold. That has
    // always been so for a collection in a property. A collection passed through a constructor was mapped before its
    // level was counted, so it was filled one level longer: with MaxDepth(1) the property was empty and the
    // constructor parameter was not. Now both are counted the same way.
    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 2)]
    public void A_collection_passed_through_a_constructor_is_left_empty_where_a_collection_property_is(int maxDepth, int expectedLines)
    {
        var mapper = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<OrderLine, OrderLineDto>();
            cfg.CreateMap<OrderWithLines, LinesPropertyDto>().MaxDepth(maxDepth);
            cfg.CreateMap<OrderWithLines, LinesRecordDto>().MaxDepth(maxDepth);
        }).CreateMapper();
        var order = new OrderWithLines { Lines = [new() { Quantity = 1 }, new() { Quantity = 2 }] };

        mapper.Map<LinesPropertyDto>(order).Lines.Count.ShouldBe(expectedLines);
        mapper.Map<LinesRecordDto>(order).Lines.Count.ShouldBe(expectedLines);
    }

    static int Count(TreeDto tree) => tree == null ? 0 : 1 + Count(tree.Left) + Count(tree.Right);

    static List<int> Values<T>(T head, Func<T, T> next, Func<T, int> value) where T : class
    {
        List<int> values = [];
        for (var node = head; node != null; node = next(node))
        {
            values.Add(value(node));
        }
        return values;
    }

    public class Branch
    {
        public int Value { get; set; }
        public Branch Risky { get; set; }
        public Branch Next { get; set; }
    }
    public class BranchDto
    {
        public int Value { get; set; }
        public BranchDto Risky { get; set; }
        public BranchDto Next { get; set; }
    }

    // A level is counted before its destination is created, and the count was taken back only after its last member
    // had been mapped. A level that failed in between stayed counted. That did not matter when the error ended the Map
    // call. It did when a resolver caught the error of a nested context.Mapper.Map and the same call went on: from then
    // on everything was cut off one level early, for each error that had been caught.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_level_that_failed_is_not_counted_when_the_error_is_caught_inside_the_same_Map_call(bool failWhileConstructing)
    {
        var mapper = new MapperConfiguration(cfg =>
        {
            var map = cfg.CreateMap<Branch, BranchDto>().MaxDepth(3)
                .ForMember(d => d.Risky, o => o.MapFrom((source, _, _, context) => MapOrNull(context, source.Risky)))
                // Mapped last, after the error below Risky has been caught.
                .ForMember(d => d.Next, o => o.SetMappingOrder(1));
            if (failWhileConstructing)
            {
                map.ConstructUsing((source, _) => source.Value < 0 ? throw new InvalidOperationException("negative") : new BranchDto());
            }
            else
            {
                map.ForMember(d => d.Value, o => o.MapFrom((source, _) => source.Value < 0 ? throw new InvalidOperationException("negative") : source.Value));
            }
        }).CreateMapper();
        Branch next = null;
        for (var value = 5; value > 0; value--)
        {
            next = new Branch { Value = value, Next = next };
        }
        var source = new Branch { Risky = new Branch { Value = -1 }, Next = next };

        var destination = mapper.Map<BranchDto>(source);

        destination.Risky.ShouldBeNull();
        // Before: 0 and 1. The level that failed below Risky was still counted, so the third level was taken for a fourth.
        Values(destination, d => d.Next, d => d.Value).ShouldBe([0, 1, 2]);
    }

    static BranchDto MapOrNull(ResolutionContext context, Branch branch)
    {
        try
        {
            return branch == null ? null : context.Mapper.Map<BranchDto>(branch);
        }
        catch (Exception)
        {
            return null;
        }
    }

    // What takes the count back runs at every level an error passes on its way out. It must not use the stack the way
    // catching and throwing again at every level did (CVE-2026-32933): these two ended the process then.
    [Fact]
    public void A_graph_too_deep_for_the_stack_still_ends_in_an_exception_when_the_map_has_a_MaxDepth()
    {
        var mapper = new MapperConfiguration(cfg => cfg.CreateMap<Node, PropertyDto>().MaxDepth(int.MaxValue)).CreateMapper();

        var exception = OnStack(256 * 1024, () => mapper.Map<PropertyDto>(Chain(100_000)));

        exception.ShouldBeAssignableTo<AutoMapperMappingException>().Message.ShouldContain("nested too deeply", Case.Sensitive);
        Values(mapper.Map<PropertyDto>(Chain(20)), d => d.Next, d => d.Value).ShouldBe(Enumerable.Range(0, 20));
    }

    [CompiledPlansFact(CompiledPlansFactAttribute.NeedsMoreStack)]
    public void An_error_deep_in_a_graph_is_still_reported_when_the_map_has_a_MaxDepth()
    {
        var mapper = new MapperConfiguration(cfg => cfg.CreateMap<Node, PropertyDto>().MaxDepth(1000)
            .ForMember(d => d.Value, o => o.MapFrom(s => FailWhenNegative(s)))).CreateMapper();
        var source = Chain(400);
        var last = source;
        while (last.Next != null)
        {
            last = last.Next;
        }
        last.Value = -1;

        var exception = OnStack(1024 * 1024, () => mapper.Map<PropertyDto>(source));

        Innermost(exception.ShouldBeAssignableTo<AutoMapperMappingException>()).Message.ShouldBe("negative");
    }

    static Exception OnStack(int maxStackSize, Action action)
    {
        Exception caught = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                caught = exception;
            }
        }, maxStackSize);
        thread.Start();
        thread.Join();
        return caught;
    }
}
