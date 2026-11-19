namespace ForkForward.Mapper.UnitTests;

// A source graph nested deeper than the stack of the thread allows used to end in a StackOverflowException,
// which cannot be caught and ends the process (CVE-2026-32933). It must end in an exception the caller can catch.
public class DeeplyNestedSourceGraphs
{
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
    public class TreeNode
    {
        public int Value { get; set; }
        public List<TreeNode> Children { get; set; } = [];
    }
    public class TreeNodeDto
    {
        public int Value { get; set; }
        public List<TreeNodeDto> Children { get; set; }
    }

    [Fact]
    public void Too_deep_for_the_stack_throws_instead_of_ending_the_process()
    {
        var mapper = new MapperConfiguration(cfg => cfg.CreateMap<Node, NodeDto>()).CreateMapper();
        var exception = OnSmallStack(() => mapper.Map<NodeDto>(Chain(100_000)));
        var mappingException = exception.ShouldBeAssignableTo<AutoMapperMappingException>();
        mappingException.Message.ShouldContain("nested too deeply", Case.Sensitive);
        mappingException.Message.ShouldContain("Next", Case.Sensitive);
        // Not wrapped once per level: an exception as deep as the graph would be as unusable as the crash.
        mappingException.InnerException.ShouldBeNull();
    }

    [Fact]
    public void The_proof_of_concept_from_the_advisory_throws_instead_of_ending_the_process()
    {
        var mapper = new MapperConfiguration(cfg => cfg.CreateMap<Node, Node>()).CreateMapper();
        var source = Chain(300_000);
        var exception = Should.Throw<AutoMapperMappingException>(() => mapper.Map<Node>(source));
        exception.Message.ShouldContain("nested too deeply", Case.Sensitive);
    }

    [Fact]
    public void A_graph_that_fits_in_the_stack_is_mapped_to_the_last_node()
    {
        var mapper = new MapperConfiguration(cfg => cfg.CreateMap<Node, NodeDto>()).CreateMapper();
        var destination = mapper.Map<NodeDto>(Chain(200));
        var length = 0;
        for (var node = destination; node != null; node = node.Next)
        {
            node.Value.ShouldBe(length);
            length++;
        }
        length.ShouldBe(200);
    }

    [Fact]
    public void Too_deep_through_collections_throws_instead_of_ending_the_process()
    {
        var mapper = new MapperConfiguration(cfg => cfg.CreateMap<TreeNode, TreeNodeDto>()).CreateMapper();
        var root = new TreeNode();
        var current = root;
        for (var i = 0; i < 100_000; i++)
        {
            var child = new TreeNode { Value = i };
            current.Children.Add(child);
            current = child;
        }
        var exception = OnSmallStack(() => mapper.Map<TreeNodeDto>(root));
        var mappingException = exception.ShouldBeAssignableTo<AutoMapperMappingException>();
        mappingException.Message.ShouldContain("nested too deeply", Case.Sensitive);
        mappingException.InnerException.ShouldBeNull();
    }

    [Fact]
    public void Too_deep_through_a_converter_that_maps_with_the_context_throws_instead_of_ending_the_process()
    {
        var mapper = new MapperConfiguration(cfg => cfg.CreateMap<Node, NodeDto>().ConvertUsing((source, _, context) =>
            new NodeDto { Value = source.Value, Next = source.Next == null ? null : context.Mapper.Map<NodeDto>(source.Next) })).CreateMapper();
        var exception = OnSmallStack(() => mapper.Map<NodeDto>(Chain(100_000)));
        exception.ShouldBeAssignableTo<AutoMapperMappingException>().Message.ShouldContain("nested too deeply", Case.Sensitive);
    }

    // The other ways from one level of a source graph to the next. Each of them goes through the mapper for every
    // level, so each ends in the same exception; no MaxDepth is configured in any of them.
    public class ConstructorNodeDto
    {
        public ConstructorNodeDto(ConstructorNodeDto next) => Next = next;
        public ConstructorNodeDto Next { get; }
    }
    public record RecordNodeDto(RecordNodeDto Next, int Value = 0);
    public class DerivedNode : Node { }
    public class DerivedNodeDto : NodeDto { }
    public class ObjectNode
    {
        public int Value { get; set; }
        public object Next { get; set; }
    }
    public interface INode
    {
        INode Next { get; }
    }
    public class InterfaceNode : INode
    {
        public int Value { get; set; }
        public INode Next { get; set; }
    }

    [Fact]
    public void Too_deep_through_a_constructor_parameter_throws_instead_of_ending_the_process()
    {
        var mapper = new MapperConfiguration(cfg => cfg.CreateMap<Node, ConstructorNodeDto>()).CreateMapper();
        ShouldBeNestedTooDeeply(OnSmallStack(() => mapper.Map<ConstructorNodeDto>(Chain(100_000))));
    }

    [Fact]
    public void Too_deep_through_the_constructor_of_a_positional_record_throws_instead_of_ending_the_process()
    {
        var mapper = new MapperConfiguration(cfg => cfg.CreateMap<Node, RecordNodeDto>()).CreateMapper();
        ShouldBeNestedTooDeeply(OnSmallStack(() => mapper.Map<RecordNodeDto>(Chain(100_000))));
    }

    [Fact]
    public void Too_deep_through_a_derived_map_throws_instead_of_ending_the_process()
    {
        var mapper = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Node, NodeDto>().Include<DerivedNode, DerivedNodeDto>();
            cfg.CreateMap<DerivedNode, DerivedNodeDto>();
        }).CreateMapper();
        Node source = null;
        for (var i = 0; i < 100_000; i++)
        {
            source = new DerivedNode { Value = i, Next = source };
        }
        ShouldBeNestedTooDeeply(OnSmallStack(() => mapper.Map<NodeDto>(source)));
    }

    [Fact]
    public void Too_deep_through_a_source_member_typed_as_object_throws_instead_of_ending_the_process()
    {
        var mapper = new MapperConfiguration(cfg => cfg.CreateMap<ObjectNode, NodeDto>()).CreateMapper();
        ObjectNode source = null;
        for (var i = 0; i < 100_000; i++)
        {
            source = new ObjectNode { Value = i, Next = source };
        }
        ShouldBeNestedTooDeeply(OnSmallStack(() => mapper.Map<NodeDto>(source)));
    }

    [Fact]
    public void Too_deep_through_a_source_member_typed_as_an_interface_throws_instead_of_ending_the_process()
    {
        var mapper = new MapperConfiguration(cfg => cfg.CreateMap<InterfaceNode, NodeDto>()).CreateMapper();
        InterfaceNode source = null;
        for (var i = 0; i < 100_000; i++)
        {
            source = new InterfaceNode { Value = i, Next = source };
        }
        ShouldBeNestedTooDeeply(OnSmallStack(() => mapper.Map<NodeDto>(source)));
    }

    [Fact]
    public void Too_deep_through_nested_dictionaries_throws_instead_of_ending_the_process()
    {
        var mapper = new MapperConfiguration(_ => { }).CreateMapper();
        Dictionary<string, object> source = null;
        for (var i = 0; i < 100_000; i++)
        {
            source = new() { ["Value"] = i, ["Next"] = source };
        }
        ShouldBeNestedTooDeeply(OnSmallStack(() => mapper.Map<NodeDto>(source)));
    }

    [Fact]
    public void Too_deep_through_nested_expando_objects_throws_instead_of_ending_the_process()
    {
        var mapper = new MapperConfiguration(_ => { }).CreateMapper();
        System.Dynamic.ExpandoObject source = null;
        for (var i = 0; i < 100_000; i++)
        {
            System.Dynamic.ExpandoObject node = new();
            IDictionary<string, object> members = node;
            members["Value"] = i;
            members["Next"] = source;
            source = node;
        }
        ShouldBeNestedTooDeeply(OnSmallStack(() => mapper.Map<NodeDto>(source)));
    }

    [Fact]
    public void Too_deep_through_ConstructUsing_that_maps_with_the_context_throws_instead_of_ending_the_process()
    {
        var mapper = new MapperConfiguration(cfg => cfg.CreateMap<Node, ConstructorNodeDto>().ConstructUsing((source, context) =>
            new ConstructorNodeDto(source.Next == null ? null : context.Mapper.Map<ConstructorNodeDto>(source.Next)))).CreateMapper();
        ShouldBeNestedTooDeeply(OnSmallStack(() => mapper.Map<ConstructorNodeDto>(Chain(100_000))));
    }

    static void ShouldBeNestedTooDeeply(Exception exception)
    {
        var mappingException = exception.ShouldBeAssignableTo<AutoMapperMappingException>();
        mappingException.Message.ShouldContain("nested too deeply", Case.Sensitive);
        mappingException.InnerException.ShouldBeNull();
    }

    [CompiledPlansFact(CompiledPlansFactAttribute.NeedsMoreStack)]
    public void An_error_deep_in_a_graph_that_fits_is_reported_instead_of_ending_the_process()
    {
        // Each level used to catch the error, wrap it and throw it again, and each of those handlers ran on top of the
        // stack that was not unwound yet: an error at depth 200 ended the process on a 1 MB stack. Now at most 8 levels wrap it.
        var mapper = new MapperConfiguration(cfg => cfg.CreateMap<Node, NodeDto>()
            .ForMember(d => d.Value, o => o.MapFrom(s => FailAtTheLastNode(s)))).CreateMapper();
        var exception = OnStack(1024 * 1024, () => mapper.Map<NodeDto>(Chain(800)));
        var wrappers = 0;
        var inner = exception;
        for (; inner is AutoMapperMappingException; inner = inner.InnerException)
        {
            wrappers++;
        }
        inner.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("the last node");
        wrappers.ShouldBe(8);
    }

    static int FailAtTheLastNode(Node node) => node.Next == null ? throw new InvalidOperationException("the last node") : node.Value;

    static Node Chain(int length)
    {
        var head = new Node();
        var current = head;
        for (var i = 1; i < length; i++)
        {
            current.Next = new Node { Value = i };
            current = current.Next;
        }
        return head;
    }

    // The depth at which the stack runs out depends on the thread; a small stack makes it quick and the same everywhere.
    static Exception OnSmallStack(Action action) => OnStack(256 * 1024, action);

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
