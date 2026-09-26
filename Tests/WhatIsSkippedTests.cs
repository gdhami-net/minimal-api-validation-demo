using System.Net;
using Xunit;

namespace ValidationDemo.Tests;

/// <summary>
/// The other half of the post: input that carries DataAnnotations attributes,
/// goes through an endpoint on an app that called AddValidation(), and is not
/// checked. Every test here asserts a 200, which is the failure the post is
/// about.
/// </summary>
public sealed class WhatIsSkippedTests : IClassFixture<DefaultApi>
{
    private readonly HttpClient _client;

    public WhatIsSkippedTests(DefaultApi factory) => _client = factory.CreateClient();

    private const string BadLines = """{"owner":"kg","lines":[{"sku":"","qty":0}]}""";

    [Fact]
    public async Task A_List_property_is_walked_into()
    {
        var response = await _client.PostAsync("/baskets/list", Http.Json(BadLines));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();
        Assert.Equal(["Lines[0].Sku", "Lines[0].Qty"], problem.Errors.Keys);
    }

    [Fact]
    public async Task An_array_property_is_stepped_over()
    {
        // Byte-for-byte the same JSON as the List test above. The only
        // difference is that BasketWithArray.Lines is LineRequest[].
        var response = await _client.PostAsync("/baskets/array", Http.Json(BadLines));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("1", await response.RawAsync());
    }

    [Theory]
    [InlineData("/baskets/list")]
    [InlineData("/baskets/ilist")]
    [InlineData("/baskets/icollection")]
    [InlineData("/baskets/ireadonlylist")]
    [InlineData("/baskets/hashset")]
    public async Task Every_other_collection_shape_is_walked_into(string route)
    {
        var response = await _client.PostAsync(route, Http.Json(BadLines));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();
        Assert.Equal(["Lines[0].Sku", "Lines[0].Qty"], problem.Errors.Keys);
    }

    [Fact]
    public async Task An_array_as_the_body_itself_is_walked_into()
    {
        // Same element type, same bad element. As the body it is checked; as the
        // Lines property of BasketWithArray it is not.
        var response = await _client.PostAsync("/lines", Http.Json("""[{"sku":"","qty":0}]"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();
        Assert.Equal(["lines[0].Sku", "lines[0].Qty"], problem.Errors.Keys);
    }

    [Fact]
    public async Task A_dictionary_property_is_stepped_over()
    {
        var response = await _client.PostAsync("/baskets/map",
            Http.Json("""{"owner":"kg","lines":{"a":{"sku":"","qty":0}}}"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("1", await response.RawAsync());
    }

    [Fact]
    public async Task The_owner_beside_a_skipped_collection_is_still_checked()
    {
        // The array is stepped over; the rest of the object is not.
        var response = await _client.PostAsync("/baskets/array",
            Http.Json("""{"owner":"","lines":[{"sku":"","qty":0}]}"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();
        Assert.Equal(["Owner"], problem.Errors.Keys);
    }

    [Fact]
    public async Task Required_on_a_nullable_parameter_does_nothing_when_it_is_absent()
    {
        var response = await _client.GetAsync("/search");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("\"(none)\"", await response.RawAsync());
    }

    [Fact]
    public async Task Required_on_a_nullable_parameter_does_fire_when_it_arrives_empty()
    {
        var response = await _client.GetAsync("/search?q=");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();
        Assert.Equal(["q"], problem.Errors.Keys);
        Assert.Equal(["The q field is required."], problem.Errors["q"]);
    }

    [Fact]
    public async Task Required_on_a_non_nullable_value_type_never_fires()
    {
        // Quantity is int and arrives as 0; Batch is int? and never arrives.
        var response = await _client.PostAsync("/counts", Http.Json("""{"quantity":0}"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();

        // Only the nullable one is reported. [Required] on int is dead weight.
        Assert.Equal(["Batch"], problem.Errors.Keys);
    }

    [Fact]
    public async Task A_nullable_value_type_parameter_is_validated_when_a_value_arrives()
    {
        // [Range(1, 10)] int? n, asked for 99.
        var response = await _client.GetAsync("/limits?n=99");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();
        Assert.Equal(["n"], problem.Errors.Keys);
    }

    [Fact]
    public async Task A_nullable_value_type_parameter_is_skipped_when_it_binds_to_null()
    {
        // Nothing to range-check, so nothing happens — the same null-skipping rule
        // that makes [Required] on a nullable parameter inert. Documented for
        // .NET 10 at github.com/dotnet/aspnetcore/issues/67033.
        var response = await _client.GetAsync("/limits");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("", await response.RawAsync());
    }

    [Fact]
    public async Task IValidatableObject_is_not_reached_while_a_property_rule_is_failing()
    {
        var bad = """{"room":"","from":"2026-09-28","to":"2026-09-01"}""";

        var withPropertyError = await _client.PostAsync("/bookings", Http.Json(bad));
        Assert.Equal(HttpStatusCode.BadRequest, withPropertyError.StatusCode);
        var first = await withPropertyError.ReadProblemAsync();

        // To is still before From, and nothing says so.
        Assert.Equal(["Room"], first.Errors.Keys);

        // Fix the room and the cross-property rule finally runs.
        var roomFixed = await _client.PostAsync("/bookings",
            Http.Json("""{"room":"B12","from":"2026-09-28","to":"2026-09-01"}"""));
        Assert.Equal(HttpStatusCode.BadRequest, roomFixed.StatusCode);
        var second = await roomFixed.ReadProblemAsync();
        Assert.Equal(["To"], second.Errors.Keys);
    }

    [Fact]
    public async Task An_endpoint_mapped_from_a_referenced_assembly_is_not_validated()
    {
        // Modules/InvoiceModule.cs maps POST /invoices. The generator only runs
        // in the assembly that calls AddValidation(), which is Api.
        var response = await _client.PostAsync("/invoices",
            Http.Json("""{"number":"","total":9999999}"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("\"\"", await response.RawAsync());
    }

    [Fact]
    public async Task Unless_that_assembly_calls_AddValidation_itself()
    {
        // Modules/ServiceCollectionExtensions.cs wraps AddValidation() so the
        // generator runs inside Modules too.
        using var factory = new ApiFactory(("Validation:Modules", "true"));
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/invoices",
            Http.Json("""{"number":"","total":9999999}"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();
        Assert.Equal(["Number", "Total"], problem.Errors.Keys);
    }

    [Fact]
    public async Task DisableValidation_opts_one_endpoint_out()
    {
        var bad = """{"reference":"","quantity":900,"email":"not-an-address"}""";

        var validated = await _client.PostAsync("/orders", Http.Json(bad));
        var notValidated = await _client.PostAsync("/orders/unchecked", Http.Json(bad));

        Assert.Equal(HttpStatusCode.BadRequest, validated.StatusCode);
        Assert.Equal(HttpStatusCode.OK, notValidated.StatusCode);
    }

    [Fact]
    public async Task The_error_key_is_the_CLR_property_name_not_the_JSON_name()
    {
        var response = await _client.PostAsync("/people", Http.Json("""{"full_name":""}"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();

        // The property is [JsonPropertyName("full_name")]. The key is not.
        Assert.Equal(["Name"], problem.Errors.Keys);

        // [Display(Name = "Customer name")] renames the field in the message only.
        Assert.Equal(["The Customer name field is required."], problem.Errors["Name"]);
    }
}
