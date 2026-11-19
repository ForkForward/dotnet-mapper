# Conditional Mapping

AutoMapper allows you to add conditions to properties that must be met before that property will be mapped.

This can be used in situations like the following where we are trying to map from an int to an unsigned int.
```c#
class Foo{
  public int baz;
}

class Bar {
  public uint baz;
}
```

In the following mapping the property baz will only be mapped if it is greater than or equal to 0 in the source object.

```c#
var configuration = new MapperConfiguration(cfg => {
  cfg.CreateMap<Foo,Bar>()
    .ForMember(dest => dest.baz, opt => opt.Condition(src => (src.baz >= 0)));
});
```
If you have a resolver, see [here](Custom-value-resolvers.md#resolvers-and-conditions) for a concrete example.
  
## Preconditions

Similarly, there is a PreCondition method. The difference is that it runs sooner in the mapping process, before the source value is resolved (think MapFrom). So the precondition is called, then we decide which will be the source of the mapping (resolving), then the condition is called and finally the destination value is assigned.

```c#
var configuration = new MapperConfiguration(cfg => {
  cfg.CreateMap<Foo,Bar>()
    .ForMember(dest => dest.baz, opt => {
        opt.PreCondition(src => (src.baz >= 0));
        opt.MapFrom(src => {
            // Expensive resolution process that can be avoided with a PreCondition
        });
    });
});
```

You can [see the steps](Understanding-your-mapping.md) yourself.

See [here](Custom-value-resolvers.md#resolvers-and-conditions) for a concrete example.

## Conditions as classes

A condition or a precondition can be a class instead of a lambda. A class can take services from the dependency injection container, and one class can be used for many members and maps.

!!! info "Written for this fork"

    This section and the next one are not inherited from AutoMapper. Condition classes exist from the release that follows 14.1.1.

A condition implements `IMemberCondition<TSource, TDestination, TMember>`. A precondition implements `IMemberPreCondition<TSource, TDestination>`. `ShouldMap` returns `true` to map the member.

```c#
using ForkForward.Mapper;

public class InStock(IWarehouse warehouse) : IMemberCondition<Order, OrderDto, int>
{
    public bool ShouldMap(Order source, OrderDto destination, int sourceMember, int destinationMember, ResolutionContext context) =>
        warehouse.Has(source.ProductId, sourceMember);
}

public class IsPublished : IMemberPreCondition<Order, OrderDto>
{
    public bool ShouldMap(Order source, OrderDto destination, ResolutionContext context) => source.Published;
}
```

```c#
cfg.CreateMap<Order, OrderDto>()
    .ForMember(dest => dest.Quantity, opt =>
    {
        opt.PreCondition<IsPublished>();
        opt.Condition<InStock>();
    });
```

They behave like the lambdas:

- A condition and a precondition on the same member both apply.
- The last `Condition` call on a member wins, whether the earlier one was a lambda or a class. The same holds for `PreCondition`.
- Derived maps inherit them.

### One class for many members

Write the class against `object`, and it fits every member:

```c#
public class SkipNulls : IMemberCondition<object, object, object>
{
    public bool ShouldMap(object source, object destination, object sourceMember, object destinationMember, ResolutionContext context) =>
        sourceMember != null;
}
```

```c#
cfg.CreateMap<Source, Destination>().ForAllMembers(opt => opt.Condition<SkipNulls>());
```

The arguments are converted to the types the class declares, so the value of an `int` member arrives boxed. A class written against `object` gets the source value before it is converted; see [What a condition sees](#what-a-condition-sees).

When the class is only known at run time, pass its type: `opt.Condition(typeof(SkipNulls))` and `opt.PreCondition(typeof(IsPublished))`. These overloads exist where the member options are not generic: maps created with `CreateMap(typeof(Source), typeof(Destination))`, `ForAllMaps` and `ForAllPropertyMaps`. A type that does not implement the interface is refused with an `ArgumentException` when the configuration is built.

### How the class is created

Like a [value resolver](Custom-value-resolvers.md) class: through the service constructor, each time the condition is evaluated.

- With `AddAutoMapper` the instance comes from the container. `AddAutoMapper` registers the conditions and preconditions it finds in the scanned assemblies as transient. A mapper resolved from a scope gives them scoped services. See [Dependency Injection](Dependency-injection.md).
- `mapper.Map(source, opt => opt.ConstructServicesUsing(...))` replaces the container for that call.
- Without a container the class needs a parameterless constructor.

### What does not work

- **`ForPath` and `ForCtorParam`.** `ForPath` has only its lambda `Condition`. A constructor parameter has no conditions at all.
- **`ReverseMap`.** A condition belongs to the map it is configured on, class or lambda. Configure it on the reverse map too.
- **`ProjectTo`.** Projections ignore conditions and preconditions, classes and lambdas.
- **A class that implements the interface more than once.** It is refused with an `ArgumentException` when the configuration is built. Write one class for each set of type arguments, or one class against `object`.
- **A class against `object` in a typed `ForMember` for a value type member.** `opt.Condition<SkipNulls>()` in `ForMember(dest => dest.Quantity, ...)` does not compile when `Quantity` is an `int`: there the class has to declare `int`. Use `ForMember("Quantity", ...)`, `ForAllMembers` or `Condition(typeof(SkipNulls))`.
- **Inheriting an open generic class.** `opt.Condition(typeof(NotDefault<>))` works on an open generic map. The class is closed like an open generic value resolver: with the type arguments of the source type, then those of the destination type. A map that inherits from the open generic map does not inherit the class, and mapping that member throws until the condition is configured on the inheriting map too. A class that is not generic is inherited as usual.
- **The name of the member in an error, where plans are interpreted.** An exception thrown by a condition class arrives without the `AutoMapperMappingException` that names the member, like an exception from a resolver that is given the context. See [Native AOT and trimming](Native-AOT-and-trimming.md#runtimes-without-dynamic-code-nothing-trimmed).

## What a condition sees

A condition is evaluated after the source value has been resolved and mapped to the type of the destination member. Which of the two values it gets as the source member depends on the type it declares for it:

- **The type of the destination member.** The condition gets the value that is about to be assigned. That is a typed `ForMember(dest => dest.Count, opt => opt.Condition((src, dest, sourceMember) => ...))`, a `ForPath` condition, and a condition class that declares the member's type.
- **`object`, or another type the source value can be passed as without a conversion.** The condition gets the source value as it was resolved, before it is converted and before value transformers. That is `ForAllMembers`, `ForMember` with a member name, maps created with `CreateMap(typeof(Source), typeof(Destination))`, `ForAllMaps`, `ForAllPropertyMaps`, and a condition class written against `object`, a base class or an interface.

The difference shows when the two members have different types:

```c#
class Source
{
    public int? Count { get; set; }
    public string Text { get; set; }
}
class Destination
{
    public int Count { get; set; }
    public int Text { get; set; }
}
```

```c#
cfg.CreateMap<Source, Destination>()
    .ForAllMembers(opt => opt.Condition((src, dest, sourceMember) => sourceMember != null));

var destination = new Destination { Count = 7 };
mapper.Map(new Source { Count = null, Text = "12" }, destination);
// destination.Count is 7: the condition got null and the member was skipped.
// destination.Text is 12: the condition got the string "12".
```

!!! warning "Changed in this fork"

    In AutoMapper 14, and in ForkForward.Mapper up to 14.1.1, such a condition got the converted value: `0` for the `null` in `Count`, the `int` 12 for `Text`. A condition that skips nulls never skipped a `null` that goes into a value type, and the destination was overwritten with the default value. In the example above `destination.Count` was 0.

What a condition that declares the source member as `object` gets:

| Source member | Destination member | Now | AutoMapper 14 |
|---|---|---|---|
| `int?` without a value | `int` | `null` | `0` |
| `bool?` without a value | `bool` | `null` | `false` |
| `string` "12" | `int` | the string "12" | the `int` 12 |
| `int` 42 | `long` | the `int` 42 | the `long` 42 |
| a `Child` | `ChildDto` | the source `Child` | the mapped `ChildDto` |
| a collection that is `null` | a collection | `null` | an empty collection, unless `AllowNullCollections` is set |
| a `string` with a value transformer | `string` | the source string | the transformed string |

What did not change:

- A condition that declares the type of the destination member gets the value that is about to be assigned.
- When both members have the same type and the value is assigned as it is, `int` to `int` or `string` to `string`, there is only one value.
- A destination member of type `object`. The condition declares the type of the destination member, so it gets the value that is about to be assigned.
- `NullSubstitute` is part of resolving the value. The condition gets the substitute, not `null`.
- The value is resolved and mapped before the condition is evaluated, also when the condition then says no. See [Resolvers and conditions](Custom-value-resolvers.md#resolvers-and-conditions).
- The destination member value that a condition gets, and preconditions.

A condition that needs the converted value declares the type of the destination member: configure it in a typed `ForMember`.
