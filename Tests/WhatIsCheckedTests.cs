using System.Net;
using Xunit;

namespace ValidationDemo.Tests;

/// <summary>
/// Every case in here is validated by the single AddValidation() call in
/// Api/Program.cs. Each test names the exact keys of the errors dictionary,
/// because the keys are the part callers bind to.
/// </summary>
public sealed class WhatIsCheckedTests : IClassFixture<DefaultApi>
{
    private readonly HttpClient _client;

    public WhatIsCheckedTests(DefaultApi factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Body_properties_are_checked_and_keyed_by_property_name()
    {
        var response = await _client.PostAsync("/orders",
            Http.Json("""{"reference":"","quantity":900,"email":"not-an-address"}"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();

        Assert.Equal("One or more validation errors occurred.", problem.Title);
        Assert.Equal(
            ["Reference", "Quantity", "Email"],
            problem.Errors.Keys);
        Assert.Equal(["The Reference field is required."], problem.Errors["Reference"]);
        Assert.Equal(["The field Quantity must be between 1 and 500."], problem.Errors["Quantity"]);
        Assert.Equal(["The Email field is not a valid e-mail address."], problem.Errors["Email"]);
    }

    [Fact]
    public async Task A_valid_body_reaches_the_handler()
    {
        var response = await _client.PostAsync("/orders",
            Http.Json("""{"reference":"A-1","quantity":3,"email":"a@b.test"}"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("\"A-1\"", await response.RawAsync());
    }

    [Fact]
    public async Task Query_parameters_are_checked_and_keyed_by_parameter_name()
    {
        var response = await _client.GetAsync("/orders?page=0&code=abcde");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();

        Assert.Equal(["page", "code"], problem.Errors.Keys);
        Assert.Equal(["The field page must be between 1 and 100."], problem.Errors["page"]);
        Assert.Equal(
            ["The field code must be a string with a maximum length of 4."],
            problem.Errors["code"]);
    }

    [Fact]
    public async Task Route_parameters_are_checked()
    {
        var response = await _client.GetAsync("/orders/99999");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();

        Assert.Equal(["id"], problem.Errors.Keys);
        Assert.Equal(["The field id must be between 1 and 10000."], problem.Errors["id"]);
    }

    [Fact]
    public async Task Header_parameters_are_checked_and_keyed_by_parameter_name_not_header_name()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/reports");
        request.Headers.TryAddWithoutValidation("X-Tenant", "far-too-long");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();

        // The key is "tenant" — the C# parameter — not "X-Tenant".
        Assert.Equal(["tenant"], problem.Errors.Keys);
        Assert.Equal(
            ["The field tenant must be a string with a maximum length of 4."],
            problem.Errors["tenant"]);
    }

    [Fact]
    public async Task Nested_objects_are_checked_with_a_dotted_key()
    {
        var response = await _client.PostAsync("/orders",
            Http.Json("""{"reference":"A-1","quantity":1,"address":{"city":"","postcode":"NW1-2AB-3CD"}}"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();

        Assert.Equal(["Address.City", "Address.Postcode"], problem.Errors.Keys);
    }

    [Fact]
    public async Task Items_of_a_List_property_are_checked_with_an_indexed_key()
    {
        var response = await _client.PostAsync("/baskets/list",
            Http.Json("""{"owner":"kg","lines":[{"sku":"OK-1","qty":2},{"sku":"","qty":0}]}"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();

        Assert.Equal(["Lines[1].Sku", "Lines[1].Qty"], problem.Errors.Keys);
    }

    [Fact]
    public async Task AsParameters_properties_are_checked()
    {
        var response = await _client.GetAsync("/pages?page=0&size=500");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();

        Assert.Equal(["Page", "Size"], problem.Errors.Keys);
    }

    [Fact]
    public async Task IValidatableObject_Validate_is_called()
    {
        var response = await _client.PostAsync("/ranges",
            Http.Json("""{"from":"2026-09-28","to":"2026-09-01"}"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();

        Assert.Equal(["To"], problem.Errors.Keys);
        Assert.Equal(["To must be on or after From."], problem.Errors["To"]);
    }

    [Fact]
    public async Task A_custom_ValidationAttribute_is_called()
    {
        var response = await _client.PostAsync("/articles",
            Http.Json("""{"slug":"Not A Slug"}"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();

        Assert.Equal(["Slug"], problem.Errors.Keys);
        Assert.Equal(
            ["Slug must be lowercase letters, digits and dashes."],
            problem.Errors["Slug"]);
    }

    [Fact]
    public async Task Form_bound_complex_parameters_are_checked()
    {
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["title"] = "",
            ["copies"] = "99",
        });

        var response = await _client.PostAsync("/uploads", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();

        Assert.Equal(["Title", "Copies"], problem.Errors.Keys);
    }

    [Fact]
    public async Task The_request_and_keys_shown_in_the_post_hero()
    {
        var response = await _client.PostAsync("/orders",
            Http.Json("""
                {"reference":"","quantity":900,"email":"nope",
                 "address":{"city":""},
                 "lines":[{"sku":"","qty":1}]}
                """));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.ReadProblemAsync();

        Assert.Equal("One or more validation errors occurred.", problem.Title);
        Assert.Equal(
            ["Reference", "Quantity", "Email", "Address.City", "Lines[0].Sku"],
            problem.Errors.Keys);
    }

    [Fact]
    public async Task Every_failing_field_is_reported_in_one_response()
    {
        var response = await _client.PostAsync("/orders",
            Http.Json("""
                {"reference":"","quantity":900,"email":"nope",
                 "address":{"city":"","postcode":"NW1-2AB-3CD"},
                 "lines":[{"sku":"","qty":0}]}
                """));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();

        Assert.Equal(
            [
                "Reference",
                "Quantity",
                "Email",
                "Address.City",
                "Address.Postcode",
                "Lines[0].Sku",
                "Lines[0].Qty",
            ],
            problem.Errors.Keys);
    }
}
