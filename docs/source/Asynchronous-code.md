# Asynchronous code

Mapping is synchronous. There is no `MapAsync`, and value resolvers, type converters, value converters and mapping actions cannot be `async`. This page says why, and how to combine mapping with code that awaits.

!!! info "Written for this fork"

    This page is not inherited from AutoMapper. It describes how the library has always worked; nothing in it is new behaviour.

## Why there is no async mapping

`async` and `await` are for waiting on something outside the process, such as a database, a file or a network call, without holding a thread. Mapping does none of that: it copies values from one object in memory to another, in well under a microsecond for an ordinary object. There is nothing to wait for, and an asynchronous call would only add work.

It is also not something that could be switched on. A mapping plan is an expression tree, and expression trees cannot contain `await`.

## Database queries: await the query, not the mapping

With `ProjectTo` the mapping becomes part of the query, and the provider runs the query asynchronously:

```c#
var orders = await dbContext.Orders
    .Where(o => o.CustomerId == customerId)
    .ProjectTo<OrderDto>(mapper.ConfigurationProvider)
    .ToListAsync(cancellationToken);
```

`ToListAsync` and the other asynchronous operators come from the query provider, for example Entity Framework Core. See [Queryable Extensions](Queryable-Extensions.md).

## A member needs data that has to be awaited

Get the data first, then map.

**Pass it into the map.** Await before the call and hand the value over with `Items`:

```c#
var unitPrice = await prices.GetUnitPriceAsync(line.ProductId, cancellationToken);

var dto = mapper.Map<Line, PricedLine>(line, opt => opt.Items["UnitPrice"] = unitPrice);
```

```c#
cfg.CreateMap<Line, PricedLine>()
    .ForMember(dest => dest.Price, opt => opt.MapFrom((src, dest, destMember, context) =>
        src.Quantity * (decimal)context.Items["UnitPrice"]));
```

For many objects, load everything in one awaited call, put a dictionary into `Items`, and look the value up in the resolver.

**Or set it after the map.** Ignore the member in the configuration and assign it yourself:

```c#
var dto = mapper.Map<PricedLine>(line);
dto.Price = await prices.GetPriceAsync(line, cancellationToken);
```

## Do not block inside a resolver

A resolver that calls `.Result`, `.Wait()` or `.GetAwaiter().GetResult()` on a task holds its thread until the task finishes. Under load that uses up the thread pool, and where there is a synchronization context it can deadlock. Use one of the two patterns above instead.

## Asynchronous streams

Map the elements as they arrive:

```c#
await foreach (var line in source.ReadLinesAsync(cancellationToken))
{
    result.Add(mapper.Map<LineDto>(line));
}
```
