using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Validation;
using Xunit;

namespace ValidationDemo.Tests;

/// <summary>
/// Which parts of the validation surface shipped as stable in .NET 10 and which
/// are still gated behind the ASP0029 "for evaluation purposes only" diagnostic.
/// Reflection, so the assertion survives a patch release changing its mind.
/// </summary>
public sealed class ExperimentalApiTests
{
    private static string? DiagnosticIdOf(MemberInfo member)
        => member.GetCustomAttribute<ExperimentalAttribute>()?.DiagnosticId;

    [Fact]
    public void AddValidation_and_DisableValidation_are_stable()
    {
        var addValidation = typeof(ValidationServiceCollectionExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == "AddValidation");

        // One overload, whose options callback is optional — AddValidation() and
        // AddValidation(o => ...) are the same method.
        Assert.Equal(2, addValidation.GetParameters().Length);
        Assert.True(addValidation.GetParameters()[1].IsOptional);
        Assert.Null(DiagnosticIdOf(addValidation));

        var disableValidation = typeof(ValidationEndpointConventionBuilderExtensions)
            .GetMethod("DisableValidation")!;
        Assert.Null(DiagnosticIdOf(disableValidation));
    }

    [Fact]
    public void ValidatableType_is_still_experimental()
    {
        // Naming the type at all is an ASP0029 error until it is suppressed —
        // which is the point of the test.
#pragma warning disable ASP0029
        var validatableType = typeof(ValidatableTypeAttribute);
#pragma warning restore ASP0029

        Assert.Equal("ASP0029", DiagnosticIdOf(validatableType));
    }

    [Fact]
    public void The_default_MaxDepth_is_32()
    {
        Assert.Equal(32, new ValidationOptions().MaxDepth);
    }
}
