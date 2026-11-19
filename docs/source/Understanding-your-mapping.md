# Understanding Your Mappings

AutoMapper creates an execution plan for your mapping. That execution plan can be viewed as [an expression tree](https://msdn.microsoft.com/en-us/library/mt654263.aspx?f=255&MSPPError=-2147217396) during debugging. You can get a better view of the resulting code by installing [the ReadableExpressions VS extension](https://marketplace.visualstudio.com/items?itemName=vs-publisher-1232914.ReadableExpressionsVisualizers). If you need to see the code outside VS, you can use [the ReadableExpressions package directly](https://www.nuget.org/packages/AgileObjects.ReadableExpressions). [This DotNetFiddle](https://dotnetfiddle.net/aJYTGZ) has a live demo  using the NuGet package, and [this article](https://agileobjects.co.uk/view-automapper-execution-plan-readableexpressions) describes using the VS extension.

```c#
var configuration = new MapperConfiguration(cfg => cfg.CreateMap<Foo, Bar>());
var executionPlan = configuration.BuildExecutionPlan(typeof(Foo), typeof(Bar));
```

Be sure to remove all such code before release.

For ProjectTo, you need to inspect `IQueryable.Expression`.

```c#
var expression = context.Entities.ProjectTo<Dto>().Expression;
```

## When a plan cannot be built

The plan of a map is built when the configuration is created. Some plans are built later, the first time they are needed: the plan of a map closed from an open generic map, and the plan for types that are only known when an object is mapped. `ProjectTo` builds an expression of its own the first time a query asks for it.

When a part of a plan cannot be built, the exception is an `AutoMapperMappingException` that names the map and the member.

!!! info "Written for this fork"

    This section is not inherited from AutoMapper. In AutoMapper 14, and in this fork up to 14.1.1, the caller got whatever was thrown while the expression was built, most often an `ArgumentException` or an `InvalidOperationException` from `System.Linq.Expressions`. Nothing in it pointed to a map.

```c#
using ForkForward.Mapper;

public class Order { public string Total { get; set; } }
public class OrderDto { public string Total { get; set; } }

public class CurrencyConverter : IValueConverter<decimal, string>
{
    public string Convert(decimal sourceMember, ResolutionContext context) => sourceMember.ToString("c");
}

var configuration = new MapperConfiguration(cfg => cfg.CreateMap<Order, OrderDto>()
    .ForMember(dest => dest.Total, opt => opt.ConvertUsing(new CurrencyConverter())));
```

The converter takes a `decimal`, and `Order.Total` is a `string`. Creating the configuration throws:

```text
ForkForward.Mapper.AutoMapperMappingException: Error building the mapping plan.

Mapping types:
Order -> OrderDto
MyApp.Order -> MyApp.OrderDto

Type Map configuration:
Order -> OrderDto
MyApp.Order -> MyApp.OrderDto

Destination Member:
Total

 ---> System.InvalidOperationException: No coercion operator is defined between types 'System.String' and 'System.Decimal'.
```

What each part means:

- **Error building the mapping plan.** The plan could not be built. For `ProjectTo` the sentence is `Error building the projection.` An error while an object is being mapped starts with `Error mapping types.` instead.
- **Mapping types** and **Type Map configuration** are the source and the destination type of the map, by name and by full name (here the types are in the namespace `MyApp`): the type arguments of `CreateMap`. The exception has them in `Types` and `TypeMap`.
- **Destination Member** is the member whose part of the plan failed: a property or a field of the destination, the path given to `ForPath` (`Customer.Name`), or a constructor parameter (`Void .ctor(System.String), parameter name`). The exception has it in `MemberMap`. This part is missing when the error belongs to the map as a whole: the construction of the destination, a before or after map action, a type converter.
- **The inner exception** is the original one, unchanged.

To find the map, search the configuration for `CreateMap<Order, OrderDto>`, or for the same two types in `CreateMap(typeof(Order), typeof(OrderDto))` and in `CreateProjection`. Four cases to know:

- A map closed from an open generic map is named with its closed types, `Wrapper<Order> -> WrapperDto<OrderDto>`. Look for `CreateMap(typeof(Wrapper<>), typeof(WrapperDto<>))`.
- When a member is mapped by another map, the exception names that map and its member, not the map that uses it. If `OrderDto.Customer` is mapped by `Customer -> CustomerDto` and `CustomerDto.Name` fails, the exception says `Customer -> CustomerDto`, member `Name`, also when it is thrown while the plan of `Order -> OrderDto` is built.
- When two types are mapped without a map of their own, by a built-in or a custom object mapper, only **Mapping types** is printed, and `TypeMap` is null.
- When the plan is built while another object is being mapped, the exception is the inner exception of the `Error mapping types.` of the member that was being mapped.

What it does not do:

- The exceptions the library throws on purpose are not wrapped and have not changed: `AutoMapperConfigurationException`, `AutoMapperMappingException`, `DuplicateTypeMapConfigurationException`.
- The maps on the way from the map that was asked for to the map that failed are not listed.
- `ProjectTo` for two types that have no map is still an `InvalidOperationException` that says `Missing map from ... to ...`, also when it is the elements of a collection member that have no map.
- A `ForPath` member that is given no `MapFrom` still fails with a bare `NullReferenceException` when the configuration is created. Asking for a map to an open generic type, such as `typeof(Destination<>)`, still fails with an `ArgumentException`.
- A map whose plan could not be built stays without a plan. Fix the map and create the configuration again; do not catch the exception and go on with the same configuration.
