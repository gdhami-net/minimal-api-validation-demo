using System.Net;
using Xunit;

namespace ValidationDemo.Tests;

/// <summary>
/// The same app with the AddValidation() call skipped. This is what a minimal
/// API did with DataAnnotations before .NET 10: nothing at all.
/// </summary>
public sealed class NoValidationTests : IClassFixture<NoValidationTests.Factory>
{
    public sealed class Factory : ApiFactory
    {
        public Factory() : base(("Validation:Enabled", "false")) { }
    }

    private readonly HttpClient _client;

    public NoValidationTests(Factory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task A_body_that_breaks_every_rule_reaches_the_handler()
    {
        var response = await _client.PostAsync("/orders",
            Http.Json("""
                {"reference":"","quantity":900,"email":"nope",
                 "address":{"city":"","postcode":"NW1-2AB-3CD"},
                 "lines":[{"sku":"","qty":0}]}
                """));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("\"\"", await response.RawAsync());
    }

    [Fact]
    public async Task An_out_of_range_query_parameter_reaches_the_handler()
    {
        var response = await _client.GetAsync("/orders?page=0&code=abcde");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("""{"page":0,"code":"abcde"}""", await response.RawAsync());
    }

    [Fact]
    public async Task IValidatableObject_is_not_called()
    {
        var response = await _client.PostAsync("/ranges",
            Http.Json("""{"from":"2026-09-28","to":"2026-09-01"}"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
