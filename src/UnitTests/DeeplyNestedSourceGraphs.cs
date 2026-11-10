namespace AutoMapper.UnitTests;

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

    [Fact]
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
