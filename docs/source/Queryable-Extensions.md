# Queryable Extensions

When using an ORM such as NHibernate or Entity Framework with AutoMapper's standard `mapper.Map` functions, you may notice that the ORM will query all the fields of all the objects within a graph when AutoMapper is attempting to map the results to a destination type.

If your ORM exposes `IQueryable`s, you can use AutoMapper's QueryableExtensions helper methods to address this key pain.

Using Entity Framework for an example, say that you have an entity `OrderLine` with a relationship with an entity `Item`. If you want to map this to an `OrderLineDTO` with the `Item`'s `Name` property, the standard `mapper.Map` call will result in Entity Framework querying the entire `OrderLine` and `Item` table.

Use this approach instead.

Given the following entities:

```c#
public class OrderLine
{
  public int Id { get; set; }
  public int OrderId { get; set; }
  public Item Item { get; set; }
  public decimal Quantity { get; set; }
}

public class Item
{
  public int Id { get; set; }
  public string Name { get; set; }
}
```

And the following DTO:

```c#
public class OrderLineDTO
{
  public int Id { get; set; }
  public int OrderId { get; set; }
  public string Item { get; set; }
  public decimal Quantity { get; set; }
}
```

You can use the Queryable Extensions like so:

```c#
var configuration = new MapperConfiguration(cfg =>
    cfg.CreateProjection<OrderLine, OrderLineDTO>()
    .ForMember(dto => dto.Item, conf => conf.MapFrom(ol => ol.Item.Name)));

public List<OrderLineDTO> GetLinesForOrder(int orderId)
{
  using (var context = new orderEntities())
  {
    return context.OrderLines.Where(ol => ol.OrderId == orderId)
             .ProjectTo<OrderLineDTO>(configuration).ToList();
  }
}
```

The `.ProjectTo<OrderLineDTO>()` will tell AutoMapper's mapping engine to emit a `select` clause to the IQueryable that will inform entity framework that it only needs to query the Name column of the Item table, same as if you manually projected your `IQueryable` to an `OrderLineDTO` with a `Select` clause.

### Query Provider Limitations

`ProjectTo` must be the last call in the LINQ method chain. ORMs work with entities, not DTOs. Apply any filtering and sorting on entities and, as the last step, project to DTOs. Query providers are highly complex and making the `ProjectTo` call last ensures the query provider works as closely as designed to build valid queries against the underlying query target (SQL, Mongo QL etc.).

Note that for this feature to work, all type conversions must be explicitly handled in your Mapping. For example, you can not rely on the `ToString()` override of the `Item` class to inform entity framework to only select from the `Name` column, and any data type changes, such as `Double` to `Decimal` must be explicitly handled as well.

### The instance API

Starting with 8.0 there are similar ProjectTo methods on IMapper that feel more natural when you use IMapper with DI.

### Preventing lazy loading/SELECT N+1 problems

Because the LINQ projection built by AutoMapper is translated directly to a SQL query by the query provider, the mapping occurs at the SQL/ADO.NET level, and not touching your entities. All data is eagerly fetched and loaded into your DTOs.

Nested collections use a Select to project child DTOs:

```c#
from i in db.Instructors
orderby i.LastName
select new InstructorIndexData.InstructorModel
{
    ID = i.ID,
    FirstMidName = i.FirstMidName,
    LastName = i.LastName,
    HireDate = i.HireDate,
    OfficeAssignmentLocation = i.OfficeAssignment.Location,
    Courses = i.Courses.Select(c => new InstructorIndexData.InstructorCourseModel
    {
        CourseID = c.CourseID,
        CourseTitle = c.Title
    }).ToList()
};
```

This map through AutoMapper will result in a SELECT N+1 problem, as each child `Course` will be queried one at a time, unless specified through your ORM to eagerly fetch. With LINQ projection, no special configuration or specification is needed with your ORM. The ORM uses the LINQ projection to build the exact SQL query needed.

That means that you don't need to use explicit eager loading (`Include`) with `ProjectTo`. If you need something like filtered `Include`, have the filter in your map:

```c#

    CreateProjection<Entity, Dto>().ForMember(d => d.Collection, o => o.MapFrom(s => s.Collection.Where(i => ...));
```

### Custom projection

In the case where members names don't line up, or you want to create calculated property, you can use MapFrom (the expression-based overload) to supply a custom expression for a destination member:

```c#
var configuration = new MapperConfiguration(cfg => cfg.CreateProjection<Customer, CustomerDto>()
    .ForMember(d => d.FullName, opt => opt.MapFrom(c => c.FirstName + " " + c.LastName))
    .ForMember(d => d.TotalContacts, opt => opt.MapFrom(c => c.Contacts.Count()));
```

AutoMapper passes the supplied expression with the built projection. As long as your query provider can interpret the supplied expression, everything will be passed down all the way to the database.

If the expression is rejected from your query provider (Entity Framework, NHibernate, etc.), you might need to tweak your expression until you find one that is accepted.

### Custom Type Conversion

Occasionally, you need to completely replace a type conversion from a source to a destination type. In normal runtime mapping, this is accomplished via the ConvertUsing method. To perform the analog in LINQ projection, use the ConvertUsing method:

```c#
cfg.CreateProjection<Source, Dest>().ConvertUsing(src => new Dest { Value = 10 });
```

The expression-based `ConvertUsing` is slightly more limited than Func-based `ConvertUsing` overloads as only what is allowed in an Expression and the underlying LINQ provider will work.

### Custom destination type constructors

If your destination type has a custom constructor but you don't want to override the entire mapping, use the ConstructUsing expression-based method overload:

```c#
cfg.CreateProjection<Source, Dest>()
    .ConstructUsing(src => new Dest(src.Value + 10));
```

AutoMapper will automatically match up destination constructor parameters to source members based on matching names, so only use this method if AutoMapper can't match up the destination constructor properly, or if you need extra customization during construction.

### String conversion

AutoMapper will automatically add `ToString()` when the destination member type is a string and the source member type is not.

```c#
public class Order {
    public OrderTypeEnum OrderType { get; set; }
}
public class OrderDto {
    public string OrderType { get; set; }
}
var orders = dbContext.Orders.ProjectTo<OrderDto>(configuration).ToList();
orders[0].OrderType.ShouldEqual("Online");
```

### Explicit expansion

In some scenarios, such as OData, a generic DTO is returned through an IQueryable controller action. Without explicit instructions, AutoMapper will expand all members in the result. To control which members are expanded during projection, set ExplicitExpansion in the configuration and then pass in the members you want to explicitly expand:

```c#
dbContext.Orders.ProjectTo<OrderDto>(configuration,
    dest => dest.Customer,
    dest => dest.LineItems);
// or string-based
dbContext.Orders.ProjectTo<OrderDto>(configuration,
    null,
    "Customer",
    "LineItems");
// for collections
dbContext.Orders.ProjectTo<OrderDto>(configuration,
    null,
    dest => dest.LineItems.Select(item => item.Product));
```
For more information, see [the tests](https://github.com/AutoMapper/AutoMapper/search?p=1&q=ExplicitExpansion&utf8=%E2%9C%93).

### Aggregations

LINQ can support aggregate queries, and AutoMapper supports LINQ extension methods. In the custom projection example, if we renamed the `TotalContacts` property to `ContactsCount`, AutoMapper would match to the `Count()` extension method and the LINQ provider would translate the count into a correlated subquery to aggregate child records.

AutoMapper can also support complex aggregations and nested restrictions, if the LINQ provider supports it:

```c#
cfg.CreateProjection<Course, CourseModel>()
    .ForMember(m => m.EnrollmentsStartingWithA,
          opt => opt.MapFrom(c => c.Enrollments.Where(e => e.Student.LastName.StartsWith("A")).Count()));
```

This query returns the total number of students, for each course, whose last name starts with the letter 'A'.

### Parameterization

Occasionally, projections need runtime parameters for their values. Consider a projection that needs to pull in the current username as part of its data. Instead of using post-mapping code, we can parameterize our MapFrom configuration:

```c#
string currentUserName = null;
cfg.CreateProjection<Course, CourseModel>()
    .ForMember(m => m.CurrentUserName, opt => opt.MapFrom(src => currentUserName));
```

When we project, we'll substitute our parameter at runtime:

```c#
dbContext.Courses.ProjectTo<CourseModel>(Config, new { currentUserName = Request.User.Name });
```

This works by capturing the name of the closure's field name in the original expression, then using an anonymous object/dictionary to apply the value to the parameter value before the query is sent to the query provider.

You may also use a dictionary to build the projection values:

```c#
dbContext.Courses.ProjectTo<CourseModel>(Config, new Dictionary<string, object> { {"currentUserName", Request.User.Name} });
```

However, using a dictionary will result in hard-coded values in the query instead of a parameterized query, so use with caution.

### Recursive models

Ideally, you would avoid models that reference themselves (do some research). But if you must, you need to enable them:

```c#
configuration.Internal().RecursiveQueriesMaxDepth = someRandomNumber;
```

`MaxDepth` on a map limits such a query further. In `ProjectTo` it is not a depth: it limits how many times the map is used in the query.

```c#
cfg.Internal().RecursiveQueriesMaxDepth = 10;
cfg.CreateProjection<Category, CategoryDto>().MaxDepth(2);

dbContext.Categories.ProjectTo<CategoryDto>(configuration);
// Select(c => new CategoryDto { Name = c.Name,
//     Parent = c.Parent == null ? null : new CategoryDto { Name = c.Parent.Name } })
```

The member that would use the map a third time is not projected, and stays `null`. What to know about it:

- **The count is for the whole query, not for one path through it.** Two members that use the same map share it: with `MaxDepth(1)` the second one is `null`.
- **With `AllowNullDestinationValues` off** the member gets an empty destination instead of `null`: the object is created, and none of its members is projected. A destination that is created through its constructor gets the default values of the parameters there: the default of an optional parameter, `default(T)` of any other. Before 15.0.0 the parameters were still projected, so a model that continues through a constructor parameter went on past the limit, as far as `RecursiveQueriesMaxDepth` allows.
- **A map that has derived maps** gives at its limit what any other map gives, for every row, whatever its type: `null`, or the empty destination of the map's own type. Its derived maps are not used there. Before 15.0.0 building such a projection failed with a `NullReferenceException`; with `AllowNullDestinationValues` off it did not fail, and the rows of a derived type went on past the limit.
- **The branch for the map itself is built before the branches for its derived maps.** In a model that refers to itself, the uses the limit allows are spent there. A row of a derived type gets its derived destination type, and its member of the base type is `null`.
- **A derived map does not inherit `MaxDepth` here.** In `Map` it does. A derived map that sets its own gives `null` for the rows of its type once it is reached. Because its branch is built last, those can be the rows of the query itself.
- **A map whose destination type is abstract is not counted.** Only its derived maps are projected, so its `MaxDepth` has no effect.

### Polymorphic projection

When a map includes derived maps (`Include`, `IncludeBase`, `IncludeAllDerived`), `ProjectTo` tests the type of every row and creates the destination type of the matching derived map:

```c#
using ForkForward.Mapper;
using ForkForward.Mapper.QueryableExtensions;

var configuration = new MapperConfiguration(cfg =>
{
    cfg.CreateMap<Animal, AnimalDto>().Include<Cat, CatDto>();
    cfg.CreateMap<Cat, CatDto>();
});

dbContext.Animals.ProjectTo<AnimalDto>(configuration);
// Select(a => a is Cat
//     ? new CatDto { Meows = (a as Cat).Meows, Name = (a as Cat).Name }
//     : new AnimalDto { Name = a.Name })
```

The type tests make the query read every derived type. With Entity Framework Core and SQL Server that is a test of the discriminator and the columns of the derived types, or, with table-per-type inheritance, a join for every derived table. Some LINQ providers cannot translate a type test at all.

!!! info "New in this fork"

    The switch below is not in AutoMapper 14. It comes with 15.0.0. Without it nothing changes: polymorphic projection is on, and the projection is the one shown above.

When the derived maps are there for `Map` and a query needs only the base members, switch polymorphic projection off for the map:

```c#
cfg.CreateMap<Animal, AnimalDto>()
    .Include<Cat, CatDto>()
    .PolymorphicProjection(false);

dbContext.Animals.ProjectTo<AnimalDto>(configuration);
// Select(a => new AnimalDto { Name = a.Name })
```

Every row is projected with that map alone. There is no type test and no cast, no member of a derived type is read, and every result is an `AnimalDto`. The SQL is the one a hand-written `Select` gives. With Entity Framework Core 9 and SQL Server, for a hierarchy stored in one table:

```sql
-- on
SELECT CASE
    WHEN [a].[Discriminator] = N'Cat' THEN CAST(1 AS bit)
    ELSE CAST(0 AS bit)
END, [a].[Meows], [a].[Name]
FROM [Animals] AS [a]

-- off
SELECT [a].[Name]
FROM [Animals] AS [a]
```

To switch it off for every map of a configuration:

```c#
using ForkForward.Mapper.Internal;

cfg.Internal().PolymorphicProjection = false;
```

A map's own setting wins over the one of the configuration, in both directions: `PolymorphicProjection(true)` keeps a map polymorphic when it is switched off for the configuration. A map without its own setting follows the configuration.

`PolymorphicProjection` is there on `CreateMap` and on `CreateProjection`.

Which map decides:

- For the query itself, the map `ProjectTo` uses for the element type of the query and the requested destination type.
- For a member, the map of the member's type. That holds for a nested object and for the elements of a collection. The map that contains the member has no say.

What it does not do:

- **It is not inherited.** A derived map does not take the setting from its base map. Each map that has derived maps decides for itself. Say the map from `Cat` to `CatDto` includes one from `Kitten` to `KittenDto`. With the map from `Animal` to `AnimalDto` switched off, a query over cats still creates a `KittenDto` for a kitten, until the cat map is switched off too. The map that `ReverseMap` creates does not take the setting either.
- **It does not choose the map of a query.** `ProjectTo<AnimalDto>` on an `IQueryable<Cat>` uses the map from `Cat` to `CatDto` and returns `CatDto` objects, on or off. That needs no type test.
- **It does not change `Map`.** In-memory mapping stays polymorphic: `mapper.Map<AnimalDto>(cat)` returns a `CatDto`.
- **It cannot be changed after the configuration is built.** Projections are cached per configuration.
- **It does not work for a map whose destination type is abstract or an interface.** With polymorphic projection on, a row that matches no derived map gets `null`. With it off there is no type left to create, and `ProjectTo` throws an `AutoMapperMappingException` that names the map, when the query is built and not when it runs. Leave it on for such a map, or tell the map what to create with `ConstructUsing` or `ConvertUsing`. A map that names its destination type with `As` is not affected: the map it names is the one that is projected.

### Supported mapping options

Not all mapping options can be supported, as the expression generated must be interpreted by a LINQ provider. Only what is supported by LINQ providers is supported by AutoMapper:
* MapFrom (Expression-based)
* ConvertUsing (Expression-based)
* Ignore
* NullSubstitute
* Value transformers
* IncludeMembers
* Runtime polymorphic mapping with Include/IncludeBase

Not supported:
* Condition
* SetMappingOrder
* UseDestinationValue
* MapFrom (Func-based)
* Before/AfterMap
* Custom resolvers
* Custom type converters
* ForPath
* Value converters
* **Any calculated property on your domain object**