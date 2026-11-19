# GHSA-6vhm-j9h7-f9gh and GHSA-gpwp-5797-mjff: routes around `MaxDepth`

| | |
|---|---|
| Advisories | [GHSA-6vhm-j9h7-f9gh](https://github.com/advisories/GHSA-6vhm-j9h7-f9gh) and [GHSA-gpwp-5797-mjff](https://github.com/advisories/GHSA-gpwp-5797-mjff), both high (CVSS 7.5), published 2026-11-05 |
| Affects | AutoMapper below 15.1.4, and 16.0.0 up to 16.2.x. Read literally, the first range includes 14.0.0, the code this fork starts from |
| Process crash in this fork | Not reproduced. `ForkForward.Mapper` 14.1.0 and later end every route the advisories describe in an exception |
| Functional defects in this fork | Two, fixed in 15.0.0: `MaxDepth` was ignored through constructor parameters and was not inherited by derived maps |
| Not covered | applications compiled with Native AOT, which the library does not support: see [CVE-2026-32933](CVE-2026-32933.md#native-aot) |
| Checked from | the public advisory texts and this repository's code only. The fixes in AutoMapper 15.1.4 and 16.3.0 are under another licence and were not looked at, as [AI_POLICY.md](../../AI_POLICY.md) requires |

## What the advisories describe

AutoMapper answered [CVE-2026-32933](CVE-2026-32933.md) with a default `MaxDepth` of 64 for types that contain themselves. A depth limit protects only where it is counted. The two advisories list the places where it was not, so that a deeply nested source graph could still use up the stack and end the process:

- the next level is passed through a constructor parameter, which includes positional records (GHSA-6vhm-j9h7-f9gh);
- the objects are of a derived type, and the derived map did not take the limit of the map it includes;
- the next level sits in a member typed `object` or as an interface, which the detection of self-referencing types does not see;
- the source is a nested `Dictionary<string, object>` or `ExpandoObject`, where depth is not counted at all;
- a type converter, or a construction function, maps the next level through the context (all four: GHSA-gpwp-5797-mjff).

## Why this fork does not crash

This fork did not answer CVE-2026-32933 with a depth limit. `Mapper` checks on every nested map call that the thread still has stack left, and stops with an `AutoMapperMappingException` when it does not. Every route above goes through a nested map call, whatever kind of member leads to the next level, so the check sees all of them.

Each route was run on this repository before anything was changed for these advisories: a chain of 100,000 levels, mapped on a thread with a 256 KB stack, no `MaxDepth` configured, one process per route.

| Route to the next level | Result |
|---|---|
| a settable property (the case of CVE-2026-32933) | exception |
| a constructor parameter of a class | exception |
| a positional record | exception |
| derived maps declared with `Include` | exception |
| a source member typed `object` | exception |
| a source member typed as an interface | exception |
| a nested `Dictionary<string, object>` mapped to a typed destination | exception |
| a nested `ExpandoObject` mapped to a typed destination | exception |
| `ConvertUsing` that maps the next level with `context.Mapper.Map` | exception |
| `ConstructUsing` that maps the next level with `context.Mapper.Map` | exception |

"Exception" is the one the fix for CVE-2026-32933 introduced: the message says that the source graph is nested too deeply and names the types being mapped.

What the check cannot see is recursion that never comes back into the mapper: a resolver or converter that walks a deep structure by calling itself. That is ordinary code outside the library, and no setting of the library limits it.

## What was wrong here

The two defects the advisories describe for `MaxDepth` itself were present in this fork as well. They could not crash the process, but the setting did not do what it says:

- With `MaxDepth(10)` on a map whose destination takes the next level through its constructor, a chain of 100 objects was mapped in full. A chain that goes through a settable property stopped at 10.
- With `MaxDepth(10)` on a base map, a chain of 100 objects of a derived type was mapped in full, with `Include` and with `IncludeBase`.

Both are fixed: the limit now also applies through constructor parameters, and a derived map takes the limit of the map it includes unless it sets its own. This changes what existing configurations produce: a graph that was mapped past the configured depth is now cut off at it. See [Configuration](../source/Configuration.md).

There is still no default depth limit. Every graph that mapped before, and has no `MaxDepth` configured, maps the same.

## Tests

`src/UnitTests/DeeplyNestedSourceGraphs.cs` has one test per route in the table above. `src/UnitTests/MaxDepthThroughConstructors.cs` and `src/UnitTests/MaxDepthInheritance.cs` cover the two defects.

## What users should do

Nothing is needed to stay protected against the crash: 14.1.0 and 14.1.1 already end these graphs in an exception. Update to 15.0.0 if a map relies on `MaxDepth` together with constructor mapping or derived maps. Code that could receive graphs of untrusted depth may want to catch `AutoMapperMappingException` around the `Map` call and reject the input.
