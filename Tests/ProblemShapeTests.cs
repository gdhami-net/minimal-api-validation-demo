using System.Net;
using Xunit;

namespace ValidationDemo.Tests;

/// <summary>
/// The 400 body AddValidation() writes on its own, and the different body you
/// get once AddProblemDetails() is also registered.
/// </summary>
public sealed class ProblemShapeTests
{
    private const string Bad = """{"reference":"","quantity":900}""";

    [Fact]
    public async Task Without_AddProblemDetails_the_body_is_plain_json_with_no_type_or_status()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/orders", Http.Json(Bad));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(
            """{"title":"One or more validation errors occurred.","errors":{"Reference":["The Reference field is required."],"Quantity":["The field Quantity must be between 1 and 500."]}}""",
            await response.RawAsync());

        var problem = await response.ReadProblemAsync();
        Assert.Null(problem.Type);
        Assert.Null(problem.Status);
    }

    [Fact]
    public async Task With_AddProblemDetails_the_body_becomes_problem_json_with_type_and_status()
    {
        using var factory = new ApiFactory(("ProblemDetails:Enabled", "true"));
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/orders", Http.Json(Bad));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.ReadProblemAsync();
        Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.5.1", problem.Type);
        Assert.Equal(400, problem.Status);
        Assert.Equal("One or more validation errors occurred.", problem.Title);
        Assert.Equal(["Reference", "Quantity"], problem.Errors.Keys);
    }
}
