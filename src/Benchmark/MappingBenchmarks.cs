using BenchmarkDotNet.Attributes;
using Benchmark.Flattening;

namespace Benchmark;

/// <summary>
/// The cost of one <c>Map</c> call once the configuration is built: the hot path of every application.
/// </summary>
public class MappingBenchmarks
{
    readonly FlatteningMapper _flattening = new();
    readonly CtorMapper _constructor = new();
    readonly ComplexTypeMapper _complex = new();
    readonly DeepTypeMapper _deep = new();
    List<Customer> _customers;

    [GlobalSetup]
    public void Setup()
    {
        _flattening.Initialize();
        _constructor.Initialize();
        _complex.Initialize();
        _deep.Initialize();
        _customers = Customers.Create(100);
        // Build the execution plans now, so that the measurement does not include compiling them.
        Flattening();
        Constructor();
        ComplexTypes();
        DeepTypes();
        Collection();
    }

    [Benchmark]
    public object Flattening() => _flattening.Map();

    [Benchmark]
    public object Constructor() => _constructor.Map();

    [Benchmark]
    public object ComplexTypes() => _complex.Map();

    [Benchmark]
    public object DeepTypes() => _deep.Map();

    [Benchmark]
    public object Collection() => Config.Map<List<Customer>, List<CustomerDTO>>(_customers);
}

/// <summary>
/// The same mappings written by hand. They do not touch the library, so baseline and current must
/// measure the same. Benchmark.ps1 uses them to tell how noisy the machine was during a run.
/// Only mappings that take about as long as the library ones belong here: one that takes a few
/// nanoseconds differs by several percent between two builds for reasons unrelated to noise.
/// </summary>
[BenchmarkCategory(Categories.Reference)]
public class ReferenceBenchmarks
{
    readonly ManualComplexTypeMapper _complex = new();
    readonly ManualDeepTypeMapper _deep = new();

    [GlobalSetup]
    public void Setup()
    {
        _complex.Initialize();
        _deep.Initialize();
    }

    [Benchmark]
    public object ComplexTypes() => _complex.Map();

    [Benchmark]
    public object DeepTypes() => _deep.Map();
}

/// <summary>
/// The cost an application pays at startup: building the configuration and compiling the execution
/// plans on the first map of each type pair. Benchmark.ps1 checks these for allocations only: their
/// timings vary too much between runs to be a gate.
/// </summary>
public class StartupBenchmarks
{
    readonly Customer _customer = Customers.Create(1)[0];
    readonly Foo _foo = Foo.New();
    readonly ModelObject _model = new() { Sub = new(), Sub2 = new(), SubWithExtraName = new() };
    readonly Model11 _model11 = new() { Value = 5 };

    [Benchmark]
    public object BuildConfiguration() => Config.CreateConfiguration();

    [Benchmark]
    public object BuildConfigurationAndMapOnce()
    {
        var mapper = Config.CreateConfiguration().CreateMapper();
        mapper.Map<ModelObject, ModelDto>(_model);
        mapper.Map<Model11, Dto11>(_model11);
        mapper.Map<Foo, FooDest>(_foo);
        return mapper.Map<Customer, CustomerDTO>(_customer);
    }
}

public static class Categories
{
    public const string Reference = "Reference";
}

static class Customers
{
    public static List<Customer> Create(int count) => Enumerable.Range(1, count).Select(id => new Customer
    {
        Id = id,
        Name = "Eduardo Najera",
        Credit = 234.7m,
        Address = new Address { City = "istanbul", Country = "turkey", Id = 1, Street = "istiklal cad." },
        HomeAddress = new Address { City = "istanbul", Country = "turkey", Id = 2, Street = "istiklal cad." },
        WorkAddresses =
        [
            new Address { City = "istanbul", Country = "turkey", Id = 5, Street = "istiklal cad." },
            new Address { City = "izmir", Country = "turkey", Id = 6, Street = "konak" },
        ],
        Addresses =
        [
            new Address { City = "istanbul", Country = "turkey", Id = 3, Street = "istiklal cad." },
            new Address { City = "izmir", Country = "turkey", Id = 4, Street = "konak" },
        ],
    }).ToList();
}
