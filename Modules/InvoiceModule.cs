using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ValidationDemo.Modules;

/// <summary>
/// A perfectly ordinary endpoint module in a class library, mapped by the host
/// app. The source generator runs in the assembly that calls AddValidation(),
/// so it never sees this signature and never writes metadata for InvoiceRequest.
/// </summary>
public static class InvoiceModule
{
    public static IEndpointRouteBuilder MapInvoices(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/invoices", (InvoiceRequest invoice) => Results.Ok(invoice.Number));
        return endpoints;
    }
}

public record InvoiceRequest
{
    [Required(AllowEmptyStrings = false)]
    public string Number { get; init; } = "";

    [Range(1, 1_000_000)]
    public decimal Total { get; init; }
}
