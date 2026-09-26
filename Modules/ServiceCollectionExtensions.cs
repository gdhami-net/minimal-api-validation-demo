using Microsoft.Extensions.DependencyInjection;

namespace ValidationDemo.Modules;

/// <summary>
/// The fix for the referenced-assembly gap: AddValidation() is called from THIS
/// assembly, so the source generator runs here and sees InvoiceRequest.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddModuleValidation(this IServiceCollection services)
        => services.AddValidation();
}
