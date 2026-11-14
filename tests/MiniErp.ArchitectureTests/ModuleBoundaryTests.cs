using System.Reflection;
using Shouldly;

namespace MiniErp.ArchitectureTests;

/// <summary>
/// Enforces the module boundaries from ADR-0001 on the compiled code, so a boundary
/// violation fails the build in CI instead of being caught (or missed) in review.
/// </summary>
public class ModuleBoundaryTests
{
    [Theory]
    [MemberData(nameof(Modules.Names), MemberType = typeof(Modules))]
    public void Module_does_not_depend_on_another_module(string moduleName)
    {
        var module = Modules.All[moduleName];
        var otherModuleNames = Modules.All
            .Where(entry => entry.Key != moduleName)
            .Select(entry => entry.Value.GetName().Name)
            .ToHashSet();

        var forbiddenReferences = ReferencedAssemblyNames(module)
            .Where(otherModuleNames.Contains)
            .ToList();

        forbiddenReferences.ShouldBeEmpty(
            $"the {moduleName} module must talk to other modules only through their public contracts (ADR-0001)");
    }

    [Fact]
    public void Platform_does_not_depend_on_any_module()
    {
        var moduleNames = Modules.All.Values
            .Select(assembly => assembly.GetName().Name)
            .ToHashSet();

        var forbiddenReferences = ReferencedAssemblyNames(Modules.Platform)
            .Where(moduleNames.Contains)
            .ToList();

        forbiddenReferences.ShouldBeEmpty("Platform is shared by every module, so it cannot depend on one");
    }

    [Theory]
    [MemberData(nameof(Modules.Names), MemberType = typeof(Modules))]
    public void Module_exposes_only_its_registration_class_and_contracts(string moduleName)
    {
        var module = Modules.All[moduleName];
        var registrationClass = $"MiniErp.{moduleName}.{moduleName}Module";
        var contractsNamespace = $"MiniErp.{moduleName}.Contracts";

        var unexpectedPublicTypes = module.GetExportedTypes()
            .Where(type => type.FullName != registrationClass)
            .Where(type => type.Namespace != contractsNamespace
                           && type.Namespace?.StartsWith(contractsNamespace + ".", StringComparison.Ordinal) != true)
            .Select(type => type.FullName)
            .ToList();

        unexpectedPublicTypes.ShouldBeEmpty(
            $"everything in the {moduleName} module except {registrationClass} and {contractsNamespace} must be internal");
    }

    private static IEnumerable<string?> ReferencedAssemblyNames(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(reference => reference.Name);
}
