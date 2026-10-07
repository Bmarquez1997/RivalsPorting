using System;
using System.Collections.Generic;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;
using RivalsPorting.Exporting.Context;
using RivalsPorting.Exporting.Models;
using RivalsPorting.Shared.Extensions;
using Serilog;

namespace RivalsPorting.Exporting;

/// <summary>
/// Collects lobby/game weapon prop meshes from a Rivals ShowBP / LikeBP CDO
/// for optional outfit export.
/// </summary>
public static class RivalsShowBpWeapons
{
    public static void AppendWeaponMeshes(ExportContext context, List<ExportMesh> meshes, UObject actorCdo, UBlueprintGeneratedClass? actorClass)
    {
        var seenComponents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenMeshPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var exported = new List<string>();
        var skippedEmpty = new List<string>();
        var skippedDuplicate = new List<string>();

        for (var index = 1; ; index++)
        {
            var propertyName = $"Weapon{index}";
            if (!actorCdo.TryGetValue(out UObject weaponComponent, propertyName))
                break;

            TryAdd(context, meshes, seenComponents, seenMeshPaths, exported, skippedEmpty, skippedDuplicate,
                propertyName, weaponComponent);
        }

        if (actorClass is not null)
        {
            foreach (var (name, component) in EnumerateWeaponComponents(actorClass))
            {
                TryAdd(context, meshes, seenComponents, seenMeshPaths, exported, skippedEmpty, skippedDuplicate,
                    name, component);
            }
        }
    }

    private static void TryAdd(
        ExportContext context,
        List<ExportMesh> meshes,
        HashSet<string> seenComponents,
        HashSet<string> seenMeshPaths,
        List<string> exported,
        List<string> skippedEmpty,
        List<string> skippedDuplicate,
        string name,
        UObject component)
    {
        if (!seenComponents.Add(name))
            return;

        var mesh = context.MeshComponent(component);
        if (mesh is null)
        {
            skippedEmpty.Add(name);
            return;
        }

        // Multiple SCS slots (e.g. WP_SquirrelsGoAcorn_01..04) often share one mesh asset.
        var meshKey = !string.IsNullOrEmpty(mesh.Path) ? mesh.Path : mesh.Name;
        if (!seenMeshPaths.Add(meshKey))
        {
            skippedDuplicate.Add($"{name}->{mesh.Name}");
            return;
        }

        meshes.Add(mesh);
        exported.Add($"{name}->{mesh.Name}");
    }

    private static IEnumerable<(string Name, UObject Component)> EnumerateWeaponComponents(UBlueprintGeneratedClass showBp)
    {
        for (UBlueprintGeneratedClass? current = showBp;
             current is not null;
             current = current.SuperStruct?.Load<UBlueprintGeneratedClass>())
        {
            if (current.TryGetValue(out UObject inheritableHandler, "InheritableComponentHandler"))
            {
                foreach (var record in inheritableHandler.GetOrDefault("Records", Array.Empty<FStructFallback>()))
                {
                    if (!record.TryGetValue(out FStructFallback componentKey, "ComponentKey"))
                        continue;

                    var name = GetStringOrName(componentKey, "SCSVariableName");
                    if (!IsWeaponComponentName(name))
                        continue;

                    if (record.TryGetValue(out UObject componentTemplate, "ComponentTemplate"))
                        yield return (name, componentTemplate);
                }
            }

            if (!current.TryGetValue(out UObject constructionScript, "SimpleConstructionScript"))
                continue;

            foreach (var node in constructionScript.GetOrDefault("AllNodes", Array.Empty<UObject>()))
            {
                var name = GetStringOrName(node, "InternalVariableName");
                if (!IsWeaponComponentName(name))
                    continue;

                if (node.TryGetValue(out UObject componentTemplate, "ComponentTemplate"))
                    yield return (name, componentTemplate);
            }
        }
    }

    /// <summary>
    /// Marvel uses both Weapon1-style slots and WP_* lobby weapon props
    /// (e.g. WP_SquirrelGirl_1032001_Squirrel_Lobby). Empty Weapon* slots are
    /// filtered later when MeshComponent returns null.
    /// </summary>
    private static bool IsWeaponComponentName(string name)
        => !string.IsNullOrEmpty(name)
           && (name.StartsWith("Weapon", StringComparison.OrdinalIgnoreCase)
               || name.StartsWith("WP_", StringComparison.OrdinalIgnoreCase));

    private static string GetStringOrName(FStructFallback obj, string propertyName)
    {
        if (obj.TryGetValue(out FName name, propertyName) && !name.IsNone)
            return name.Text;
        return obj.GetOrDefault(propertyName, string.Empty);
    }

    private static string GetStringOrName(UObject obj, string propertyName)
    {
        if (obj.TryGetValue(out FName name, propertyName) && !name.IsNone)
            return name.Text;
        return obj.GetOrDefault(propertyName, string.Empty);
    }
}
