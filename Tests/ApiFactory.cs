using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ValidationDemo.Tests;

/// <summary>
/// Boots the Api project in-process. Settings let one test class run the app
/// with AddValidation() present, another with it absent, and a third with
/// AddProblemDetails() on top.
/// </summary>
public class ApiFactory(params (string Key, string Value)[] settings)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Pinned so the depth test's 500 carries the developer exception page
        // whatever ASPNETCORE_ENVIRONMENT says on the machine running this.
        builder.UseEnvironment("Development");

        // Keeps the test output readable. The MaxDepth test deliberately causes
        // an unhandled exception, and logging it would bury the run summary.
        builder.UseSetting("Logging:LogLevel:Default", "Warning");
        builder.UseSetting("Logging:LogLevel:Microsoft.AspNetCore.Diagnostics", "None");

        foreach (var (key, value) in settings)
        {
            builder.UseSetting(key, value);
        }
    }
}

/// <summary>The app exactly as Program.cs writes it: AddValidation(), nothing else.</summary>
public sealed class DefaultApi : ApiFactory;

/// <summary>The bits of the 400 body the tests care about.</summary>
public sealed record Problem(
    string? Type,
    string? Title,
    int? Status,
    Dictionary<string, string[]> Errors,
    string? ContentType);

public static class Http
{
    private static readonly JsonSerializerOptions Options =
        new(JsonSerializerDefaults.Web);

    public static StringContent Json(string body)
        => new(body, Encoding.UTF8, "application/json");

    public static async Task<Problem> ReadProblemAsync(this HttpResponseMessage response)
    {
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(Options);
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (json.TryGetProperty("errors", out var errorsNode))
        {
            foreach (var field in errorsNode.EnumerateObject())
            {
                errors[field.Name] = [.. field.Value.EnumerateArray().Select(m => m.GetString()!)];
            }
        }

        return new Problem(
            Type: json.TryGetProperty("type", out var type) ? type.GetString() : null,
            Title: json.TryGetProperty("title", out var title) ? title.GetString() : null,
            Status: json.TryGetProperty("status", out var status) ? status.GetInt32() : null,
            Errors: errors,
            ContentType: response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>The error keys, in the order the response listed them.</summary>
    public static async Task<string> RawAsync(this HttpResponseMessage response)
        => await response.Content.ReadAsStringAsync();
}
