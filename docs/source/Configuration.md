# Configuration

Create a `MapperConfiguration` instance and initialize configuration via the constructor:

```c#
var config = new MapperConfiguration(cfg => {
    cfg.CreateMap<Foo, Bar>();
    cfg.AddProfile<FooProfile>();
});
```

The `MapperConfiguration` instance can be stored statically, in a static field or in a dependency injection container. Once created it cannot be changed/modified.

```c#
var configuration = new MapperConfiguration(cfg => {
    cfg.CreateMap<Foo, Bar>();
    cfg.AddProfile<FooProfile>();
});
```
Starting with 9.0, the static API is no longer available.

## Profile Instances

A good way to organize your mapping configurations is with profiles.
Create classes that inherit from `Profile` and put the configuration in the constructor:
```c#
// This is the approach starting with version 5
public class OrganizationProfile : Profile
{
	public OrganizationProfile()
	{
		CreateMap<Foo, FooDto>();
		// Use CreateMap... Etc.. here (Profile methods are the same as configuration methods)
	}
}

// How it was done in 4.x - as of 5.0 this is obsolete:
// public class OrganizationProfile : Profile
// {
//     protected override void Configure()
//     {
//         CreateMap<Foo, FooDto>();
//     }
// }
```

In earlier versions the `Configure` method was used instead of a constructor.
As of version 5, `Configure()` is obsolete. It will be removed in 6.0.

Configuration inside a profile only applies to maps inside the profile. Configuration applied to the root configuration applies to *all* maps created.

### Assembly Scanning for auto configuration

Profiles can be added to the main mapper configuration in a number of ways, either directly:

```
cfg.AddProfile<OrganizationProfile>();
cfg.AddProfile(new OrganizationProfile());
```

or by automatically scanning for profiles:

```c#
// Scan for all profiles in an assembly
// ... using instance approach:
var config = new MapperConfiguration(cfg => {
    cfg.AddMaps(myAssembly);
});
var configuration = new MapperConfiguration(cfg => cfg.AddMaps(myAssembly));

// Can also use assembly names:
var configuration = new MapperConfiguration(cfg =>
    cfg.AddMaps(new [] {
        "Foo.UI",
        "Foo.Core"
    });
);

// Or marker types for assemblies:
var configuration = new MapperConfiguration(cfg =>
    cfg.AddMaps(new [] {
        typeof(HomeController),
        typeof(Entity)
    });
);
```

AutoMapper will scan the designated assemblies for classes inheriting from Profile and add them to the configuration.

## Naming Conventions

You can set the source and destination naming conventions

```c#
var configuration = new MapperConfiguration(cfg => {
  cfg.SourceMemberNamingConvention = LowerUnderscoreNamingConvention.Instance;
  cfg.DestinationMemberNamingConvention = PascalCaseNamingConvention.Instance;
});
```

This will map the following properties to each other:
`  property_name -> PropertyName `

A run of capital letters in a PascalCase destination member is read as one word, and its last capital starts the next word when a lower case letter follows it: `form_xml -> FormXML`, `xml_file -> XMLFile`, `https_connection -> HTTPSConnection`. A digit stays in the word it follows: `html5_parser -> HTML5Parser`. The reading in which every capital starts a word (`a_b_count -> ABCount`) is tried first, so where both find a source member, that one is used.

[Flattening](Flattening.md) follows the words of the destination member through the source types at any depth, in both directions: `customer.address.city.name -> CustomerAddressCityName` and `Customer.Address.City.Name -> customer_address_city_name`. Where a chain of two source members matches the name as well (`customer_address.city_name`), that one is used.

You can also set this per profile

```c#
public class OrganizationProfile : Profile
{
  public OrganizationProfile()
  {
    SourceMemberNamingConvention = LowerUnderscoreNamingConvention.Instance;
    DestinationMemberNamingConvention = PascalCaseNamingConvention.Instance;
    //Put your CreateMap... Etc.. here
  }
}
```
If you don't need a naming convention, you can use the `ExactMatchNamingConvention`.

## Replacing characters

You can also replace individual characters or entire words in source members during member name matching:

```c#
public class Source
{
    public int Value { get; set; }
    public int Ävíator { get; set; }
    public int SubAirlinaFlight { get; set; }
}
public class Destination
{
    public int Value { get; set; }
    public int Aviator { get; set; }
    public int SubAirlineFlight { get; set; }
}
```

We want to replace the individual characters, and perhaps translate a word:

```c#
var configuration = new MapperConfiguration(c =>
{
    c.ReplaceMemberName("Ä", "A");
    c.ReplaceMemberName("í", "i");
    c.ReplaceMemberName("Airlina", "Airline");
});
```

## Recognizing pre/postfixes

Sometimes your source/destination properties will have common pre/postfixes that cause you to have to do a bunch of custom member mappings because the names don't match up. To address this, you can recognize pre/postfixes:

```c#
public class Source {
    public int frmValue { get; set; }
    public int frmValue2 { get; set; }
}
public class Dest {
    public int Value { get; set; }
    public int Value2 { get; set; }
}
var configuration = new MapperConfiguration(cfg => {
    cfg.RecognizePrefixes("frm");
    cfg.CreateMap<Source, Dest>();
});
configuration.AssertConfigurationIsValid();
```

By default AutoMapper recognizes the prefix "Get", if you need to clear the prefix:

```c#
var configuration = new MapperConfiguration(cfg => {
    cfg.ClearPrefixes();
    cfg.RecognizePrefixes("tmp");
});
```

## Global property/field filtering

By default, AutoMapper tries to map every public property/field. You can filter out properties/fields with the property/field filters:

```c#
var configuration = new MapperConfiguration(cfg =>
{
	// don't map any fields
	cfg.ShouldMapField = fi => false;

	// map properties with a public or private getter
	cfg.ShouldMapProperty = pi =>
		pi.GetMethod != null && (pi.GetMethod.IsPublic || pi.GetMethod.IsPrivate);
});
```

## Configuring visibility

By default, AutoMapper only recognizes public members. It can map to private setters, but will skip internal/private methods and properties if the entire property is private/internal. To instruct AutoMapper to recognize members with other visibilities, override the default filters ShouldMapField and/or ShouldMapProperty :

```c#
var configuration = new MapperConfiguration(cfg =>
{
    // map properties with public or internal getters
    cfg.ShouldMapProperty = p => p.GetMethod.IsPublic || p.GetMethod.IsAssembly;
    cfg.CreateMap<Source, Destination>();
});
```

Map configurations will now recognize internal/private members.

## Configuration compilation

Because expression compilation can be a bit resource intensive, AutoMapper lazily compiles the type map plans on first map. However, this behavior is not always desirable, so you can tell AutoMapper to compile its mappings directly:

```c#
var configuration = new MapperConfiguration(cfg => {});
configuration.CompileMappings();
```

For a few hundred mappings, this may take a couple of seconds. If it's a lot more than that, you probably have some really big execution plans.

### Long compilation times

Compilation times increase with the size of the execution plan and that depends on the number of properties and their complexity. Ideally, you would fix your model so you have many small DTOs, each for a particular use case. But you can also decrease the size of the execution plan without changing your classes.

You can set `MapAtRuntime` per member or `MaxExecutionPlanDepth` globally (the default is one, set it to zero).

These will reduce the size of the execution plan by replacing the execution plan for a child object with a method call. The compilation will be faster, but the mapping itself might be slower. Search the repo for more details and use a profiler to better understand the effect.
Avoiding `PreserveReferences` and `MaxDepth` also helps.
## Source graphs too deep for the stack

A type that contains itself (a tree, a linked list) is mapped one level at a time, and each level uses about a kilobyte of the thread's stack. A graph nested deeper than the stack allows ends in an `AutoMapperMappingException` that says so, naming the member being mapped. On a thread with the default 1 MB stack that is roughly a thousand levels; a 256 KB stack gives about 140, an 8 MB one about 9,000.

If a graph of that depth is legitimate, map it on a thread with a larger stack (`new Thread(work, maxStackSize)`), or set `MaxDepth` on the type map to stop at a fixed depth. Where the depth comes from outside the application, the exception is the intended outcome: before this fork the same input ended in a `StackOverflowException`, which cannot be caught and ends the process ([CVE-2026-32933](https://github.com/advisories/GHSA-rvv3-g6hj-g44x)).

A mapping error deep in a graph is wrapped in an `AutoMapperMappingException` by each of the innermost 8 levels, each wrapper naming the member that was being mapped, and then passed up unchanged. Wrapping at every level used to end the process for an error a few hundred levels deep.

### `MaxDepth`

`MaxDepth` limits how many objects of one type map are mapped inside one another:

```c#
cfg.CreateMap<Category, CategoryDto>().MaxDepth(3);
```

A category, its parent and the parent's parent are mapped. The member that would start a fourth level is set to `null`. The collection members of the object on the third level are left empty, whatever they hold; arrays are the exception: they are mapped, with `null` for each element that would be a fourth level.

How the depth is counted:

- Each type map counts for itself, within one `Map` call. The count is the number of objects of that map that are being mapped at that moment, one inside another; the elements of a collection are all on the same level. A level that fails is no longer counted either: a resolver, a converter or a destination factory that catches the error of a nested `context.Mapper.Map` call and maps on gets the same depth as before the error.
- A level is counted before its destination object is created, so it makes no difference whether the next level is reached through a property, through a constructor parameter (of a positional record, for example) or by `ConstructUsing` that maps with the context. Before the release that follows 14.1.1, as in AutoMapper 14, the limit was ignored on the last two routes and the whole graph was mapped. The same holds for the collections of the last level: one that is passed through a constructor is left empty where one in a property is. With `MaxDepth(1)` that is every collection of the map, of any element type; before, a collection in a constructor parameter was still filled there.
- A derived map that sets no `MaxDepth` takes it from the map it inherits from, through `Include`, `IncludeBase` or `IncludeAllDerived`. Over several levels of inheritance the nearest map that sets one wins, and a `MaxDepth` set on the derived map itself always wins. Before the release that follows 14.1.1 a derived map had no limit unless it set one; to keep it that way, give the derived map a limit that no graph reaches, such as `MaxDepth(int.MaxValue)`.
- A map can inherit from several maps that have a `MaxDepth`: a class that implements two interfaces, each with its own map, or a map that both a base map and the base of that map include. Which limit it takes does not depend on the order of the `Include`, `IncludeBase` or `CreateMap` calls. A more derived map beats a less derived one, whether it set its limit itself or inherited it; an open generic base map counts as the base of the maps of the types derived from it. Of the maps that are left, which are not related to each other, one that set its `MaxDepth` itself beats one that inherited it, and of those that are still tied the smallest `MaxDepth` wins.
- The limit is inherited, the count is not shared. With `MaxDepth(3)` on a base map and a derived map that inherits it, a chain that alternates base and derived objects is mapped 6 levels deep: 3 of each map.

`MaxDepth` has no effect on a map with `ConvertUsing`, where the converter does the mapping. The map created by `ReverseMap` does not get it: call `MaxDepth` again after `ReverseMap` if the reverse map needs one. In `ProjectTo` it is not a depth: it limits how many times the map is used in the query, and a derived map does not inherit it there.

A use past that limit gives `null` or, with `AllowNullDestinationValues` off, an empty destination, whether the next level is reached through a property or through a constructor parameter; see [Recursive models](Queryable-Extensions.md#recursive-models).

`MaxDepth` decides what the result contains. It is not what keeps a deep graph from ending the process: that is the check described above, which is always on and needs no configuration. A `MaxDepth` larger than the stack allows ends in the same exception as no `MaxDepth` at all.
