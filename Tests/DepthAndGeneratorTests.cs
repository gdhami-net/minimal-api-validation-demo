using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;
using ValidationDemo.Api;
using Xunit;

namespace ValidationDemo.Tests;

/// <summary>
/// The depth ceiling, and the reflection fact behind one claim in the post:
/// where a positional record keeps the attributes you wrote on it.
/// </summary>
public sealed class DepthAndGeneratorTests
{
    private static string Chain(int depth)
    {
        var json = new StringBuilder();
        for (var i = 0; i < depth; i++)
        {
            json.Append("""{"label":"x","next":""");
        }
        json.Append("null");
        json.Append('}', depth);
        return json.ToString();
    }

    [Fact]
    public async Task A_graph_inside_the_default_depth_is_validated_normally()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        // 31 nested levels, the innermost one carrying an empty label.
        var json = Chain(31).Replace("""{"label":"x","next":null}""", """{"label":"","next":null}""");
        var response = await client.PostAsync("/chains", Http.Json(json));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();
        Assert.Single(problem.Errors);
        Assert.EndsWith("Next.Label", problem.Errors.Keys.Single());
    }

    [Fact]
    public async Task Past_the_default_depth_of_32_validation_throws()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/chains", Http.Json(Chain(40)));

        // Not a 400 with a field list: the validator gives up and throws, which
        // reaches the client as a 500.
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains(
            "System.InvalidOperationException: Maximum validation depth of 32 exceeded at",
            await response.RawAsync());
        Assert.Contains("in 'ChainRequest'", await response.RawAsync());
    }

    [Fact]
    public async Task MaxDepth_is_configurable_through_the_AddValidation_overload()
    {
        using var factory = new ApiFactory(("Validation:MaxDepth", "64"));
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/chains", Http.Json(Chain(40)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void A_positional_records_attributes_live_on_the_constructor_parameter()
    {
        var property = typeof(ShipmentRequest).GetProperty(nameof(ShipmentRequest.Carrier))!;
        var parameter = typeof(ShipmentRequest).GetConstructors()
            .Single(c => c.GetParameters().Length == 2)
            .GetParameters()[0];

        // The compiler leaves the attribute where you wrote it: on the primary
        // constructor parameter, not on the property it generates.
        Assert.Empty(property.GetCustomAttributes(typeof(ValidationAttribute), inherit: true));
        Assert.Single(parameter.GetCustomAttributes(typeof(RequiredAttribute), inherit: true));
    }

    [Fact]
    public async Task And_the_endpoint_still_rejects_it()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/shipments",
            Http.Json("""{"carrier":"","parcels":0}"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();
        Assert.Equal(["Carrier", "Parcels"], problem.Errors.Keys);
    }
}
