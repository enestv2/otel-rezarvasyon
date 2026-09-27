using System.Reflection;
using RPAOtelRezervasyon.Application.Orchestration;
using RPAOtelRezervasyon.Domain.Models;
using RPAOtelRezervasyon.Infrastructure.Persistence.Mongo;

namespace RPAOtelRezervasyon.ArchitectureTests;

/// <summary>
/// docs/architecture.md "Yasak bağımlılıklar" kurallarını derleme/signature düzeyinde doğrular.
/// </summary>
public sealed class ArchitectureTests
{
    private static readonly Assembly DomainAssembly = typeof(ContractedHotel).Assembly;

    private static readonly Assembly ApplicationAssembly = typeof(RecommendationRequest).Assembly;

    private static readonly Assembly InfrastructureAssembly = typeof(MongoGeocodeCache).Assembly;

    [Fact]
    public void Core_layers_do_not_reference_infrastructure_or_api()
    {
        foreach (var assembly in new[] { DomainAssembly, ApplicationAssembly })
        {
            var referenced = assembly.GetReferencedAssemblies().Select(name => name.Name ?? string.Empty);

            Assert.DoesNotContain(
                referenced,
                name => name is "RPAOtelRezervasyon.Infrastructure" or "RPAOtelRezervasyon.Api");
        }
    }

    [Fact]
    public void Domain_does_not_use_http_provider_or_mongo_types()
    {
        foreach (var type in DomainAssembly.GetTypes())
        {
            Assert.DoesNotContain(
                ReferencedTypes(type),
                referenced => IsForbiddenForCoreLayer(referenced)
                    || IsForbiddenNameFragment(referenced.Name)
                    || referenced.Namespace?.Contains(".Providers.", StringComparison.Ordinal) == true);
        }
    }

    [Fact]
    public void Application_does_not_use_http_mongo_or_json_serialization_types()
    {
        foreach (var type in ApplicationAssembly.GetTypes())
        {
            Assert.DoesNotContain(
                ReferencedTypes(type),
                referenced => IsForbiddenForCoreLayer(referenced)
                    || IsForbiddenNameFragment(referenced.Name)
                    || referenced.Namespace?.Contains(".Providers.", StringComparison.Ordinal) == true);
        }
    }

    [Fact]
    public void Persistence_namespace_does_not_use_http_client()
    {
        var persistenceTypes = InfrastructureAssembly.GetTypes()
            .Where(type => type.Namespace?.Contains(".Persistence", StringComparison.Ordinal) == true);

        foreach (var type in persistenceTypes)
        {
            Assert.DoesNotContain(
                ReferencedTypes(type),
                referenced => referenced == typeof(HttpClient)
                    || referenced.Namespace?.StartsWith("System.Net.Http", StringComparison.Ordinal) == true);
        }
    }

    [Fact]
    public void Providers_namespace_does_not_use_mongo_driver()
    {
        var providerTypes = InfrastructureAssembly.GetTypes()
            .Where(type => type.Namespace?.Contains(".Providers", StringComparison.Ordinal) == true);

        foreach (var type in providerTypes)
        {
            Assert.DoesNotContain(
                ReferencedTypes(type),
                referenced => referenced.Namespace?.StartsWith("MongoDB", StringComparison.Ordinal) == true);
        }
    }

    private static bool IsForbiddenForCoreLayer(Type referenced) =>
        referenced.Namespace?.StartsWith("System.Net", StringComparison.Ordinal) == true
        || referenced.Namespace?.StartsWith("MongoDB", StringComparison.Ordinal) == true
        || referenced.Namespace?.StartsWith("System.Text.Json", StringComparison.Ordinal) == true
        || referenced.Namespace?.StartsWith("Newtonsoft.Json", StringComparison.Ordinal) == true
        || referenced.Namespace?.StartsWith("QuestPDF", StringComparison.Ordinal) == true
        || referenced.Namespace?.StartsWith("SkiaSharp", StringComparison.Ordinal) == true
        || referenced.Namespace?.StartsWith("UglyToad", StringComparison.Ordinal) == true;

    private static bool IsForbiddenNameFragment(string name) =>
        name.Contains("HttpClient", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Mongo", StringComparison.OrdinalIgnoreCase);

    /// <summary>Bir tipin taban tipi, arayüzleri, alanları, özellikleri ve metot imzalarındaki tipler.</summary>
    private static IEnumerable<Type> ReferencedTypes(Type type)
    {
        const BindingFlags flags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
            BindingFlags.Static | BindingFlags.DeclaredOnly;

        var roots = new List<Type> { type };
        if (type.BaseType is not null)
        {
            roots.Add(type.BaseType);
        }

        roots.AddRange(type.GetInterfaces());
        roots.AddRange(type.GetFields(flags).Select(field => field.FieldType));
        roots.AddRange(type.GetProperties(flags).Select(property => property.PropertyType));
        roots.AddRange(type.GetMethods(flags).SelectMany(method =>
            new[] { method.ReturnType }.Concat(method.GetParameters().Select(parameter => parameter.ParameterType))));
        roots.AddRange(type.GetConstructors(flags)
            .SelectMany(constructor => constructor.GetParameters().Select(parameter => parameter.ParameterType)));

        return roots.SelectMany(Flatten).Distinct();
    }

    private static IEnumerable<Type> Flatten(Type type)
    {
        yield return type;

        if (!type.IsGenericType)
        {
            yield break;
        }

        foreach (var argument in type.GetGenericArguments().SelectMany(Flatten))
        {
            yield return argument;
        }
    }
}
