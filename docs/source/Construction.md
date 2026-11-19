# Construction

AutoMapper can map to destination constructors based on source members:

```c#
public class Source {
    public int Value { get; set; }
}
public class SourceDto {
    public SourceDto(int value) {
        _value = value;
    }
    private int _value;
    public int Value {
        get { return _value; }
    }
}
var configuration = new MapperConfiguration(cfg => cfg.CreateMap<Source, SourceDto>());
```

If the destination constructor parameter names don't match, you can modify them at config time:

```c#
public class Source {
    public int Value { get; set; }
}
public class SourceDto {
    public SourceDto(int valueParamSomeOtherName) {
        _value = valueParamSomeOtherName;
    }
    private int _value;
    public int Value {
        get { return _value; }
    }
}
var configuration = new MapperConfiguration(cfg =>
  cfg.CreateMap<Source, SourceDto>()
    .ForCtorParam("valueParamSomeOtherName", opt => opt.MapFrom(src => src.Value))
);
```

This works for both LINQ projections and in-memory mapping.

You can also disable constructor mapping:    

```c#
var configuration = new MapperConfiguration(cfg => cfg.DisableConstructorMapping());
```

You can configure which constructors are considered for the destination object:

```c#
// use only public constructors
var configuration = new MapperConfiguration(cfg => cfg.ShouldUseConstructor = constructor => constructor.IsPublic);
```
When mapping to records, consider using only public constructors.

## Creating the destination with a factory class

A lambda passed to `ConstructUsing` cannot get services from a dependency injection container. A factory class can: the mapper creates it through the service constructor, like a class-based value resolver or type converter, so it can take dependencies in its constructor.

!!! info "Added in this fork"

    `IDestinationFactory<TSource, TDestination>` is not part of AutoMapper 14. It is available from the release that follows 14.1.1.

```c#
using ForkForward.Mapper;

public class Order {
    public int Id { get; set; }
    public decimal Total { get; set; }
}
public class OrderDto {
    public OrderDto(string currency) => Currency = currency;
    public string Currency { get; }
    public int Id { get; set; }
    public decimal Total { get; set; }
}
public class OrderDtoFactory : IDestinationFactory<Order, OrderDto> {
    private readonly ICurrencies _currencies;
    public OrderDtoFactory(ICurrencies currencies) => _currencies = currencies;

    public OrderDto Create(Order source, ResolutionContext context) => new OrderDto(_currencies.CurrencyOf(source.Id));
}
public class OrderProfile : Profile {
    public OrderProfile() => CreateMap<Order, OrderDto>().ConstructUsing<OrderDtoFactory>();
}
```

The factory creates the object, and the mapper then maps the members onto it as usual: in the example the factory sets `Currency`, the mapper sets `Id` and `Total`.

The factory replaces constructor mapping for that map. The destination type needs no constructor that the mapper could use, and configuration validation does not ask for one.

A new factory is created each time a destination object has to be created:

- With `services.AddAutoMapper(...)` it comes from the container, which is how `OrderDtoFactory` gets its `ICurrencies`. See [Dependency injection](Dependency-injection.md).
- `mapper.Map<Order, OrderDto>(order, opt => opt.ConstructServicesUsing(type => ...))` replaces the container for that call.
- Without a container, and without `cfg.ConstructServicesUsing(...)`, the factory is created with its parameterless constructor.

The factory is not called when the caller passes an existing destination object, as in `mapper.Map(order, existingDto)`, and not for a `null` source.

For a map created from types, or when the factory type is only known at run time, pass the type:

```c#
cfg.CreateMap(typeof(Order), typeof(OrderDto)).ConstructUsing(typeof(OrderDtoFactory));
```

That type is checked when the configuration is created. It has to implement `IDestinationFactory<TSource, TDestination>` with a `TSource` that the source type of the map can be assigned to and a `TDestination` that can be assigned to its destination type; otherwise the configuration throws an `AutoMapperConfigurationException` that names the factory and the map. A factory written for a base source type, or one that returns a derived destination type, fits: the interface is variant, so the generic overload accepts it too.

A factory class can implement the interface more than once. The implementation for exactly the source and the destination type of the map is the one that is used. Without one, exactly one implementation has to fit: when several do, nothing says which of them was meant, and the configuration throws an `AutoMapperConfigurationException` that lists them.

On a map created from types, and in `ForAllMaps`, the mapping expression has no type arguments, so the compiler can check `ConstructUsing<TDestinationFactory>()` against `IDestinationFactory<object, object>` only: a factory for `object` is accepted whatever it returns, and what it returns is cast to the destination type when an object is mapped. `ConstructUsing(Type)` accepts such a factory there too, when what it returns is a base type of the destination type, `object` for example; an implementation that fits the types of the map without a cast is still preferred. Either way, a factory that returns an object of another type fails with an `InvalidCastException` when an object is mapped. On `CreateMap<Order, OrderDto>()` neither overload accepts a factory that returns a base type of `OrderDto`: the compiler refuses the generic one, and the configuration throws for the other.

An open generic map takes an open generic factory. It is closed the way an open generic type converter is (see [Open Generics](Open-Generics.md)): with the type arguments of the source type, followed by those of the destination type.

```c#
public class DestinationFactory<T> : IDestinationFactory<Source<T>, Destination<T>> {
    public Destination<T> Create(Source<T> source, ResolutionContext context) => new Destination<T>();
}

cfg.CreateMap(typeof(Source<>), typeof(Destination<>)).ConstructUsing(typeof(DestinationFactory<>));
```

For an open generic map the types can only be checked for each closed map, so a factory that does not fit `Source<int>` and `Destination<int>` fails when those two are first mapped, not when the configuration is created.

An open generic factory can also be given through `ForAllMaps`, to the open generic maps among them: for each closed map it is closed the same way, with the type arguments of the sides that are open in the open generic map. A map that was not closed from an open generic map has no such side, and the configuration throws an `AutoMapperConfigurationException` (`Cannot close the open generic destination factory ...`) when it is created.

In everything else a factory behaves like the `ConstructUsing((source, context) => ...)` lambda:

- The last `ConstructUsing` or `ConstructUsingServiceLocator` call on a map wins.
- `PreserveReferences`, `MaxDepth`, `BeforeMap` and `AfterMap` work the same. With `PreserveReferences` the factory is called once for each source object.

What does not work, again as with that lambda:

- **`ProjectTo` does not use the factory.** A query provider cannot call it. `ProjectTo` creates the destination with its parameterless constructor, and throws an `ArgumentException` (`Type '...' does not have a default constructor`) when there is none. For projections use the expression overload, `ConstructUsing(src => new OrderDto(...))`, described in [Queryable Extensions](Queryable-Extensions.md#custom-destination-type-constructors); a map has only one of the two.
- **Derived maps do not inherit it.** With `Include`, `IncludeBase` or `IncludeAllDerived` the derived map creates its destination its own way. Give the derived map its own `ConstructUsing`.
- **A struct destination is always created by the factory**, also when the caller passes one to `Map`.
- **The factory must not return `null`.** Mapping the members then fails with an `AutoMapperMappingException`.
- **There is no attribute for it.** `AutoMapAttribute` cannot name a factory; use a profile.