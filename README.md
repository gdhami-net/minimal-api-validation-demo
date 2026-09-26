# minimal-api-validation-demo

Companion repo for **"Minimal API validation in .NET 10: what AddValidation checks
and what it skips"** ([gdhami.net](https://gdhami.net) — link added when the post
is live).

.NET 10 added built-in DataAnnotations validation to minimal APIs. One call turns
it on:

```csharp
builder.Services.AddValidation();
```

That is the whole feature as far as your code is concerned. `Api/Contracts.cs`
contains nothing but ordinary `System.ComponentModel.DataAnnotations` attributes,
and `Api/Program.cs` maps ordinary endpoints. A source generator
(`Microsoft.Extensions.Validation.ValidationsGenerator`, shipped as an analyzer in
the `Microsoft.AspNetCore.App.Ref` pack) walks out from the endpoint signatures at
build time and intercepts the `AddValidation()` call with a resolver for the types
it found.

The point of this repo is the boundary. Several kinds of input carry attributes,
go through a validated endpoint, and are still not checked. The tests assert
those cases as passing requests, because a 200 is exactly the surprise.

Measured on **SDK 10.0.201, ASP.NET Core runtime 10.0.5**, Windows 11. The
`Api` project is booted in-process by `WebApplicationFactory`; nothing listens on
a real port.

## What the suite asserts

### Checked (`Tests/WhatIsCheckedTests.cs`)

| Input | Key in the `errors` dictionary |
|---|---|
| JSON body property | `Reference` — the CLR property name |
| Nested object property | `Address.City` |
| Item of a `List<T>` property | `Lines[1].Sku` |
| Query parameter | `page` — the C# parameter name |
| Route parameter | `id` |
| Header parameter, `[FromHeader(Name = "X-Tenant")] string? tenant` | `tenant`, **not** `X-Tenant` |
| `[AsParameters]` record struct property | `Page` |
| `IValidatableObject.Validate` | whatever `MemberNames` says |
| A custom `ValidationAttribute` | the property name |
| `[FromForm]` complex parameter | `Title` |
| A positional record's parameter attributes | `Carrier` |

All failing fields come back in one response, not one at a time:

```json
{"title":"One or more validation errors occurred.","errors":{
  "Reference":["The Reference field is required."],
  "Quantity":["The field Quantity must be between 1 and 500."],
  "Email":["The Email field is not a valid e-mail address."],
  "Address.City":["The City field is required."],
  "Address.Postcode":["The field Postcode must be a string with a maximum length of 8."],
  "Lines[0].Sku":["The Sku field is required."],
  "Lines[0].Qty":["The field Qty must be between 1 and 99."]}}
```

### Skipped (`Tests/WhatIsSkippedTests.cs`)

- **An array-typed property.** `BasketWithList.Lines` is `List<LineRequest>` and
  `BasketWithArray.Lines` is `LineRequest[]`. The two endpoints get byte-identical
  JSON. The `List` one answers 400 with `Lines[0].Sku`; the array one answers 200.
  `IList<T>`, `ICollection<T>`, `IReadOnlyList<T>` and `HashSet<T>` all behave like
  `List<T>` (one `[Theory]` covers all five) — the array is the odd one out.
  `POST /lines`, whose body *is* a `LineRequest[]`, does return 400: it is arrays
  reached through a property that are stepped over.
- **A dictionary-typed property.** `Dictionary<string, LineRequest>` values are
  never visited, even though `LineRequest` is a type the generator knows. This one
  fails later than the array case: the generator does record the property, and
  emits an entry for the dictionary type with a member called `this[]`.
- **The rest of the object is unaffected.** `BasketWithArray.Owner` still returns
  400 while the array beside it is skipped, which is what makes this easy to miss.
- **A parameter that bound to null.** `GET /search` with `[Required] string? q`
  answers 200 when `q` is absent and 400 when it arrives empty (`?q=`). Same for
  `GET /limits` with `[Range(1, 10)] int? n`: 400 for `?n=99`, 200 for nothing at
  all. The nullable-value-type half of this is a documented .NET 10 limitation
  ([dotnet/aspnetcore #67033](https://github.com/dotnet/aspnetcore/issues/67033)).
- **`[Required]` on a non-nullable value type.** `[Required] int Quantity` cannot
  fail, because `0` is not null. `[Required] int? Batch` does fail. Old
  DataAnnotations behaviour, newly visible here.
- **`IValidatableObject.Validate` while a property rule is failing.** `POST /bookings`
  with an empty `Room` *and* `To` before `From` reports only `Room`. Fix the room
  and the cross-property error appears. Class-level validation runs after property
  validation and only if it succeeded.
- **An endpoint mapped from a referenced assembly.** `Modules/InvoiceModule.cs`
  maps `POST /invoices` from a class library. The generator only runs in the
  assembly that calls `AddValidation()`, so `InvoiceRequest` has no metadata and
  the endpoint accepts anything. `Modules/ServiceCollectionExtensions.cs` shows
  the fix — a wrapper around `AddValidation()` inside that assembly, called from
  the host — and `Validation:Modules=true` turns it on for one test.
- **`DisableValidation()`** opts one endpoint out while its neighbour still validates.
- **The error key is the CLR property name.** `[JsonPropertyName("full_name")]`
  changes the wire name and not the key; `[Display(Name = "Customer name")]`
  changes the message and not the key.

### The response shape (`Tests/ProblemShapeTests.cs`)

With `AddValidation()` alone the 400 body is **`application/json`** and carries only
`title` and `errors`:

```json
{"title":"One or more validation errors occurred.","errors":{"Reference":["The Reference field is required."],"Quantity":["The field Quantity must be between 1 and 500."]}}
```

Add `builder.Services.AddProblemDetails()` and the same request produces
**`application/problem+json`** with `type`, `status` and a `traceId`:

```json
{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"One or more validation errors occurred.","status":400,"errors":{"Reference":["The Reference field is required."],"Quantity":["The field Quantity must be between 1 and 500."]},"traceId":"..."}
```

### Without the call (`Tests/NoValidationTests.cs`)

The same app with `AddValidation()` skipped (`Validation:Enabled=false`) accepts a
body that breaks every rule in it, and never calls `IValidatableObject.Validate`.
That is what a minimal API did before .NET 10.

### The depth ceiling (`Tests/DepthAndGeneratorTests.cs`)

`ValidationOptions.MaxDepth` defaults to 32. A 40-deep self-referencing graph is
not a 400 — validation throws, and the request ends as a 500:

```text
System.InvalidOperationException: Maximum validation depth of 32 exceeded at
'Next.Next.…Next' in 'ChainRequest'. This is likely caused by a circular
reference in the object graph. Consider increasing the MaxDepth in
ValidationOptions if deeper validation is required.
```

`AddValidation(options => options.MaxDepth = 64)` lets the same graph through.

### Stable versus experimental (`Tests/ExperimentalApiTests.cs`)

`AddValidation` and `DisableValidation` carry no `[Experimental]` attribute.
`ValidatableTypeAttribute` carries `[Experimental("ASP0029")]`, so naming
`[ValidatableType]` is a compiler **error** until you suppress ASP0029 —
`Tests/ExperimentalApiTests.cs` does exactly that around one line.

## Reading the generated metadata

The array gap is visible in the generator's own output:

```bash
dotnet build Api -t:Rebuild -p:EmitCompilerGeneratedFiles=true -p:CompilerGeneratedFilesOutputPath=../_gen
```

Then open
`_gen/Microsoft.Extensions.Validation.ValidationsGenerator/Microsoft.Extensions.Validation.ValidationsGenerator/ValidatableInfoResolver.g.cs`.
`BasketWithList` is emitted with two members, `Owner` and `Lines`;
`BasketWithArray` is emitted with one:

```csharp
if (type == typeof(global::ValidationDemo.Api.BasketWithArray))
{
    validatableInfo = new GeneratedValidatableTypeInfo(
        type: typeof(global::ValidationDemo.Api.BasketWithArray),
        members: [
            new GeneratedValidatablePropertyInfo(
                containingType: typeof(global::ValidationDemo.Api.BasketWithArray),
                propertyType: typeof(string),
                name: "Owner",
                displayName: "Owner"
            ),
        ]
    );
```

Delete `_gen` afterwards — leaving it in place makes the next build fail, because
the generated file gets picked up as a compile item and the interceptor no longer
matches.

## Running it

```bash
./check.sh            # or: .\check.ps1 on Windows
```

or directly:

```bash
dotnet test
```

Requires a .NET 10 SDK. 46 tests, well under a second.

```text
Test Run Successful.
Total tests: 46
     Passed: 46
```

To poke at it by hand instead:

```bash
dotnet run --project Api
curl -s -X POST http://localhost:5000/orders \
  -H 'content-type: application/json' \
  -d '{"reference":"","quantity":900,"email":"nope"}'
```

## Layout

```
Api/Program.cs       the endpoints, and the one AddValidation() call
Api/Contracts.cs     the request types — plain DataAnnotations, nothing else
Modules/             endpoints in a class library, and the wrapper that fixes them
Tests/               the six test classes described above
check.sh, check.ps1  run the suite
```

## Licence

MIT.
