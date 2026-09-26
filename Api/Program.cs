using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using ValidationDemo.Api;
using ValidationDemo.Modules;

var builder = WebApplication.CreateBuilder(args);

// The whole feature. Everything the tests assert follows from this one line
// being present (or, for the NoValidation tests, absent).
if (builder.Configuration.GetValue("Validation:Enabled", true))
{
    builder.Services.AddValidation(options =>
    {
        var depth = builder.Configuration.GetValue("Validation:MaxDepth", 0);
        if (depth > 0)
        {
            options.MaxDepth = depth;
        }
    });
}

// Off by default, so the tests can show the endpoints in Modules going
// unvalidated and then being validated once their own assembly registers.
if (builder.Configuration.GetValue("Validation:Modules", false))
{
    builder.Services.AddModuleValidation();
}

// Off by default, so the tests can show both response shapes.
if (builder.Configuration.GetValue("ProblemDetails:Enabled", false))
{
    builder.Services.AddProblemDetails();
}

var app = builder.Build();

// --- checked: a JSON body, its nested object and its List<T> ----------------
app.MapPost("/orders", (OrderRequest order) => Results.Ok(order.Reference));

// --- checked: query, route and header parameters ----------------------------
app.MapGet("/orders", ([Range(1, 100)] int page, [StringLength(4)] string? code)
    => Results.Ok(new { page, code }));

app.MapGet("/orders/{id}", ([Range(1, 10_000)] int id) => Results.Ok(id));

app.MapGet("/reports", ([FromHeader(Name = "X-Tenant")][StringLength(4)] string? tenant)
    => Results.Ok(tenant ?? "(none)"));

// --- checked: [AsParameters] ------------------------------------------------
app.MapGet("/pages", ([AsParameters] PageArgs args) => Results.Ok(args));

// --- checked: IValidatableObject and a custom ValidationAttribute -----------
app.MapPost("/ranges", (DateRange range) => Results.Ok(range));
app.MapPost("/bookings", (BookingRequest booking) => Results.Ok(booking.Room));
app.MapPost("/articles", (ArticleRequest article) => Results.Ok(article.Slug));

// --- checked: a form-bound complex parameter -------------------------------
app.MapPost("/uploads", ([FromForm] UploadRequest upload) => Results.Ok(upload.Title))
   .DisableAntiforgery();

// --- checked: attributes left on a record's constructor parameters ----------
app.MapPost("/shipments", (ShipmentRequest shipment) => Results.Ok(shipment.Carrier));

// --- the three baskets: only the collection type differs --------------------
app.MapPost("/baskets/list", (BasketWithList basket) => Results.Ok(basket.Lines.Count));
app.MapPost("/baskets/array", (BasketWithArray basket) => Results.Ok(basket.Lines.Length));
app.MapPost("/baskets/map", (BasketWithMap basket) => Results.Ok(basket.Lines.Count));

app.MapPost("/baskets/ilist", (BasketWithIList b) => Results.Ok(b.Lines.Count));
app.MapPost("/baskets/icollection", (BasketWithICollection b) => Results.Ok(b.Lines.Count));
app.MapPost("/baskets/ireadonlylist", (BasketWithIReadOnlyList b) => Results.Ok(b.Lines.Count));
app.MapPost("/baskets/hashset", (BasketWithHashSet b) => Results.Ok(b.Lines.Count));

// An array as the body itself, rather than as a property of the body.
app.MapPost("/lines", (LineRequest[] lines) => Results.Ok(lines.Length));

// --- skipped: a nullable parameter that never arrived ----------------------
app.MapGet("/search", ([Required] string? q) => Results.Ok(q ?? "(none)"));

// --- skipped: a nullable value type parameter (aspnetcore #67033) ----------
app.MapGet("/limits", ([Range(1, 10)] int? n) => Results.Ok(n));

// --- skipped: [Required] on a non-nullable value type ----------------------
app.MapPost("/counts", (CountRequest count) => Results.Ok(count.Quantity));

// --- the error key is the C# property name, not the JSON name --------------
app.MapPost("/people", (PersonRequest person) => Results.Ok(person.Name));

// --- opted out for this endpoint only --------------------------------------
app.MapPost("/orders/unchecked", (OrderRequest order) => Results.Ok(order.Reference))
   .DisableValidation();

// --- the MaxDepth ceiling --------------------------------------------------
app.MapPost("/chains", (ChainRequest chain) => Results.Ok(chain.Label));

// --- skipped: an endpoint mapped from a referenced class library ------------
app.MapInvoices();

app.Run();
