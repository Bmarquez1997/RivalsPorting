using System;
using System.Collections.Generic;
using System.Linq;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.Utils;
using RivalsPorting.Exporting.Context;
using RivalsPorting.Exporting.Models;
using RivalsPorting.Exporting.Styles;
using RivalsPorting.Exporting.Types;

namespace RivalsPorting.Exporting;

public static class RivalsEmoteWeaponProps
{
    private const string EmoteTablePath = "Marvel/Content/Marvel/Data/DataTable/UI/HeroSkin/UIHeroEmoteTable";
    private const string SkinTablePath = "Marvel/Content/Marvel/Data/DataTable/HeroGallery/UISkinTable";

    public static void AppendFromEmote(ExportContext exporter, List<ExportProp> props, FStructFallback emote, UBlueprintGeneratedClass showBp)
    {
        if (!emote.TryGetValue(out FStructFallback[] weaponAnims, "WeaponAnim") || weaponAnims.Length == 0)
            return;

        foreach (var weaponAnim in weaponAnims)
        {
            var weaponName = GetStringOrName(weaponAnim, "WeaponName");
            if (string.IsNullOrEmpty(weaponName)) continue;

            var component = FindComponentByVariableName(showBp, weaponName);
            if (component is null) continue;

            var mesh = exporter.MeshComponent(component);
            if (mesh is null) continue;

            var animSections = new List<ExportAnimSection>();
            if (GetEmoteAnimationPath(weaponAnim) is { } weaponAnimPath
                && UEParse.Provider.TryLoadPackageObject(weaponAnimPath, out var weaponAnimAsset)
                && weaponAnimAsset is UAnimSequenceBase weaponSequence
                && exporter.AnimSequence(weaponSequence) is { } section)
            {
                animSections.Add(section);
            }

            props.Add(new ExportProp
            {
                Mesh = mesh,
                AnimSections = animSections,
                SocketName = FindAttachSocket(showBp, weaponName) ?? string.Empty,
                LocationOffset = FVector.ZeroVector,
                RotationOffset = FRotator.ZeroRotator,
                Scale = FVector.OneVector
            });
        }
    }

    public static void AppendForExportedAnim(ExportContext exporter, List<ExportProp> props, UObject animAsset, ExportStyleBase[] styles)
    {
        var animPath = ResolveExportedAnimPath(animAsset, styles);
        if (string.IsNullOrEmpty(animPath)) return;

        var emote = FindEmoteByAnimPath(animPath);
        if (emote is null) return;
        if (!emote.TryGetValue(out FStructFallback identifier, "EmoteIdentifier")) return;

        var heroId = identifier.GetOrDefault("HeroID", string.Empty);
        var shapeId = identifier.GetOrDefault("ShapeID", "0");
        if (string.IsNullOrEmpty(heroId)) return;

        var showBp = ResolveDefaultShowActor(heroId, shapeId);
        if (showBp is null) return;

        AppendFromEmote(exporter, props, emote, showBp);
    }

    public static UBlueprintGeneratedClass? ResolveShowActorFromStyles(ExportStyleBase[] styles)
    {
        foreach (var style in styles.OfType<ExportStructStyle>())
        {
            if (style.StyleData.TryGetValue(out UBlueprintGeneratedClass showActorClass, "ShowActorClass"))
                return showActorClass;
        }

        return null;
    }

    public static void ImportLobbyPose(MeshExport export, ExportContext exporter, ExportStyleBase[] styles)
    {
        string? heroId = null;
        string? shapeId = null;

        if (styles.OfType<ExportRivalsFormStyle>().FirstOrDefault() is { } formStyle)
        {
            heroId = formStyle.HeroId;
            shapeId = formStyle.ShapeId;
        }

        foreach (var style in styles.OfType<ExportStructStyle>())
        {
            if (!style.StyleData.TryGetValue(out FStructFallback identifier, "Identifier"))
                continue;

            heroId ??= identifier.GetOrDefault("HeroID", string.Empty);
            shapeId ??= identifier.GetOrDefault("ShapeID", "0");
            if (!string.IsNullOrEmpty(heroId))
                break;
        }

        if (string.IsNullOrEmpty(heroId)
            || !UEParse.Provider.TryLoadPackageObject<UDataTable>(EmoteTablePath, out var emoteTable)
            || emoteTable.RowMap is null)
        {
            return;
        }

        shapeId ??= "0";
        foreach (var emote in emoteTable.RowMap.Values)
        {
            if (!emote.TryGetValue(out FStructFallback identifier, "EmoteIdentifier")
                || identifier.GetOrDefault("EmoteID", string.Empty) != "201"
                || identifier.GetOrDefault("SkinID", string.Empty) != "001"
                || identifier.GetOrDefault("HeroID", string.Empty) != heroId
                || identifier.GetOrDefault("ShapeID", "0") != shapeId
                || GetEmoteAnimationPath(emote) is not { } animPath
                || !UEParse.Provider.TryLoadPackageObject(animPath, out var animAsset)
                || animAsset is null)
            {
                continue;
            }

            export.Animation = new AnimExport(animAsset.Name, animAsset, [], EExportType.Animation, exporter.Meta, null);
            if (ResolveShowActorFromStyles(styles) is { } showBp)
                AppendFromEmote(exporter, export.Animation.Props, emote, showBp);
            return;
        }
    }

    public static UBlueprintGeneratedClass? ResolveDefaultShowActor(string heroId, string shapeId)
    {
        if (!UEParse.Provider.TryLoadPackageObject<UDataTable>(SkinTablePath, out var skinTable)
            || skinTable.RowMap is null)
        {
            return null;
        }

        UBlueprintGeneratedClass? fallback = null;
        foreach (var skin in skinTable.RowMap.Values)
        {
            if (!skin.TryGetValue(out FStructFallback identifier, "Identifier"))
                continue;

            if (identifier.GetOrDefault("HeroID", string.Empty) != heroId)
                continue;
            if (identifier.GetOrDefault("SkinID", string.Empty) != "001")
                continue;

            if (!skin.TryGetValue(out UBlueprintGeneratedClass showActorClass, "ShowActorClass"))
                continue;

            var rowShapeId = identifier.GetOrDefault("ShapeID", "0");
            if (rowShapeId == shapeId)
                return showActorClass;

            if (rowShapeId == "0")
                fallback = showActorClass;
        }

        return fallback;
    }

    public static UBlueprintGeneratedClass? ResolveShowActorBySkinItemId(string skinItemId, string preferredShapeId = "0")
    {
        if (string.IsNullOrEmpty(skinItemId)
            || !UEParse.Provider.TryLoadPackageObject<UDataTable>(SkinTablePath, out var skinTable)
            || skinTable.RowMap is null)
        {
            return null;
        }

        UBlueprintGeneratedClass? fallback = null;
        foreach (var skin in skinTable.RowMap.Values)
        {
            if (!string.Equals(skin.GetOrDefault("SkinItemID", string.Empty), skinItemId, StringComparison.Ordinal))
                continue;

            if (!skin.TryGetValue(out UBlueprintGeneratedClass showActorClass, "ShowActorClass"))
                continue;

            var shapeId = "0";
            if (skin.TryGetValue(out FStructFallback identifier, "Identifier"))
                shapeId = identifier.GetOrDefault("ShapeID", "0");

            if (shapeId == preferredShapeId)
                return showActorClass;

            fallback ??= showActorClass;
        }

        return fallback;
    }

    public static UObject? FindComponentByVariableName(UBlueprintGeneratedClass showBp, string variableName)
        => FindWeaponComponent(showBp, variableName);

    public static string? FindAttachSocket(UBlueprintGeneratedClass showBp, string variableName)
        => FindWeaponAttachSocket(showBp, variableName);

    public static FStructFallback? FindEmoteByAnimPath(string animPath)
    {
        if (!UEParse.Provider.TryLoadPackageObject<UDataTable>(EmoteTablePath, out var emoteTable)
            || emoteTable.RowMap is null)
        {
            return null;
        }

        var normalized = NormalizeAnimPath(animPath);
        foreach (var emote in emoteTable.RowMap.Values)
        {
            if (GetEmoteAnimationPath(emote) is not { } path)
                continue;

            if (string.Equals(NormalizeAnimPath(path), normalized, StringComparison.OrdinalIgnoreCase))
                return emote;
        }

        return null;
    }

    public static string? GetEmoteAnimationPath(FStructFallback emote)
    {
        if (emote.TryGetValue(out FSoftObjectPath animMT, "AnimMT")
            && !animMT.AssetPathName.IsNone
            && !string.IsNullOrEmpty(animMT.AssetPathName.Text))
        {
            return animMT.AssetPathName.Text;
        }

        if (emote.TryGetValue(out FSoftObjectPath anim, "Anim")
            && !anim.AssetPathName.IsNone
            && !string.IsNullOrEmpty(anim.AssetPathName.Text))
        {
            return anim.AssetPathName.Text;
        }

        return null;
    }

    private static string? ResolveExportedAnimPath(UObject animAsset, ExportStyleBase[] styles)
    {
        if (styles.OfType<ExportObjectStyle>().FirstOrDefault() is { } animStyle)
            return animStyle.StyleData.GetPathName();

        return animAsset.GetPathName();
    }

    private static string NormalizeAnimPath(string path)
    {
        // Soft paths use "/Game/..."; package object paths may include class prefixes.
        var text = path.SubstringAfter(":");
        if (string.IsNullOrEmpty(text) || text == path)
            text = path;
        return text.Replace('\\', '/').Trim();
    }

    private static UObject? FindWeaponComponent(UBlueprintGeneratedClass showBp, string weaponName)
    {
        for (UBlueprintGeneratedClass? current = showBp; current is not null; current = current.SuperStruct?.Load<UBlueprintGeneratedClass>())
        {
            if (current.TryGetValue(out UObject inheritableHandler, "InheritableComponentHandler"))
            {
                foreach (var record in inheritableHandler.GetOrDefault("Records", Array.Empty<FStructFallback>()))
                {
                    if (!record.TryGetValue(out FStructFallback componentKey, "ComponentKey"))
                        continue;

                    if (GetStringOrName(componentKey, "SCSVariableName") != weaponName)
                        continue;

                    if (record.TryGetValue(out UObject componentTemplate, "ComponentTemplate"))
                        return componentTemplate;
                }
            }

            if (current.TryGetValue(out UObject constructionScript, "SimpleConstructionScript"))
            {
                foreach (var node in constructionScript.GetOrDefault("AllNodes", Array.Empty<UObject>()))
                {
                    if (GetStringOrName(node, "InternalVariableName") != weaponName)
                        continue;

                    if (node.TryGetValue(out UObject componentTemplate, "ComponentTemplate"))
                        return componentTemplate;
                }
            }
        }

        return null;
    }

    private static string? FindWeaponAttachSocket(UBlueprintGeneratedClass showBp, string weaponName)
    {
        for (UBlueprintGeneratedClass? current = showBp; current is not null; current = current.SuperStruct?.Load<UBlueprintGeneratedClass>())
        {
            if (!current.TryGetValue(out UObject constructionScript, "SimpleConstructionScript"))
                continue;

            foreach (var node in constructionScript.GetOrDefault("AllNodes", Array.Empty<UObject>()))
            {
                if (GetStringOrName(node, "InternalVariableName") != weaponName)
                    continue;

                var attachName = GetStringOrName(node, "AttachToName");
                if (!string.IsNullOrEmpty(attachName))
                    return attachName;
            }
        }

        return null;
    }

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
