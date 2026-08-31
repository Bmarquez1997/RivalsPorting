using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Exports.LevelSequence;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;
using RivalsPorting.Exporting.Context;
using RivalsPorting.Exporting.Models;
using RivalsPorting.Shared.Extensions;

namespace RivalsPorting.Exporting;

public static class RivalsMvpExport
{
    private static readonly Regex SkinItemIdFromPath = new(
        @"/Characters/\d+/(\d+)/",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static void AppendFromLevelSequence(
        ExportContext exporter,
        UObject levelSequenceAsset,
        ref ExportMesh? skeleton,
        List<ExportAnimSection> sections,
        List<ExportProp> props)
    {
        var movieScene = ResolveMovieScene(levelSequenceAsset);
        if (movieScene is null) return;

        var bindings = movieScene.GetOrDefault("ObjectBindings", Array.Empty<FStructFallback>());
        if (bindings.Length == 0) return;

        var showBp = ResolveShowBp(levelSequenceAsset);
        var characterAnims = new List<UAnimSequenceBase>();
        var propBindings = new List<(string BindingName, List<UAnimSequenceBase> Anims)>();

        foreach (var binding in bindings)
        {
            var bindingName = GetStringOrName(binding, "BindingName");
            if (string.IsNullOrEmpty(bindingName)) continue;

            var anims = CollectSkeletalAnims(binding);
            if (anims.Count == 0) continue;

            if (IsCharacterBinding(bindingName))
                characterAnims.AddRange(anims);
            else
                propBindings.Add((bindingName, anims));
        }

        // Fallback: first skeletal track is the character if Mesh1 was missing.
        if (characterAnims.Count == 0 && propBindings.Count > 0)
        {
            characterAnims.AddRange(propBindings[0].Anims);
            propBindings.RemoveAt(0);
        }

        foreach (var anim in characterAnims)
        {
            if (skeleton is null && anim.Skeleton.Load<USkeleton>() is { } skel)
                skeleton = exporter.Skeleton(skel);

            sections.AddIfNotNull(exporter.AnimSequence(anim));
        }

        if (showBp is null) return;

        foreach (var (bindingName, anims) in propBindings)
        {
            var component = RivalsEmoteWeaponProps.FindComponentByVariableName(showBp, bindingName);
            if (component is null) continue;

            var mesh = exporter.MeshComponent(component);
            if (mesh is null) continue;

            var animSections = new List<ExportAnimSection>();
            foreach (var anim in anims)
                animSections.AddIfNotNull(exporter.AnimSequence(anim));

            props.Add(new ExportProp
            {
                Mesh = mesh,
                AnimSections = animSections,
                SocketName = RivalsEmoteWeaponProps.FindAttachSocket(showBp, bindingName) ?? string.Empty,
                LocationOffset = FVector.ZeroVector,
                RotationOffset = FRotator.ZeroRotator,
                Scale = FVector.OneVector
            });
        }
    }

    private static UObject? ResolveMovieScene(UObject levelSequenceAsset)
    {
        if (levelSequenceAsset is ULevelSequence levelSequence
            && levelSequence.MovieScene.TryLoad(out UObject typedMovieScene))
        {
            return typedMovieScene;
        }

        if (levelSequenceAsset.TryGetValue(out FPackageIndex movieSceneIndex, "MovieScene")
            && movieSceneIndex.TryLoad(out UObject movieScene))
        {
            return movieScene;
        }

        if (levelSequenceAsset.TryGetValue(out UObject movieSceneObject, "MovieScene"))
            return movieSceneObject;

        return null;
    }

    private static UBlueprintGeneratedClass? ResolveShowBp(UObject levelSequenceAsset)
    {
        var skinItemId = ExtractSkinItemId(levelSequenceAsset);
        if (string.IsNullOrEmpty(skinItemId)) return null;
        return RivalsEmoteWeaponProps.ResolveShowActorBySkinItemId(skinItemId);
    }

    private static string? ExtractSkinItemId(UObject levelSequenceAsset)
    {
        var path = levelSequenceAsset.GetPathName().Replace('\\', '/');
        var match = SkinItemIdFromPath.Match(path);
        if (match.Success)
            return match.Groups[1].Value;

        // LS_1032305301_MVP → 1032305
        var name = levelSequenceAsset.Name;
        if (name.StartsWith("LS_", StringComparison.OrdinalIgnoreCase))
        {
            var digits = new string(name.Skip(3).TakeWhile(char.IsDigit).ToArray());
            if (digits.Length >= 7)
                return digits[..7];
        }

        return null;
    }

    private static bool IsCharacterBinding(string bindingName)
        => bindingName.Equals("Mesh1", StringComparison.OrdinalIgnoreCase)
           || bindingName.Equals("Mesh", StringComparison.OrdinalIgnoreCase);

    private static List<UAnimSequenceBase> CollectSkeletalAnims(FStructFallback binding)
    {
        // Level sequences often place the same AnimSequence in many timeline sections
        // (different SectionRanges / offsets). Export each unique asset once.
        var anims = new List<UAnimSequenceBase>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var track in LoadTrackObjects(binding))
        {
            if (!IsSkeletalAnimationTrack(track)) continue;

            foreach (var section in LoadSectionObjects(track))
            {
                if (!TryGetSectionAnimation(section, out var anim))
                    continue;

                if (!seenPaths.Add(anim.GetPathName()))
                    continue;

                anims.Add(anim);
            }
        }

        return anims;
    }

    private static bool IsSkeletalAnimationTrack(UObject track)
        => track.ExportType.Contains("SkeletalAnimation", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<UObject> LoadTrackObjects(FStructFallback binding)
    {
        if (binding.TryGetValue(out FPackageIndex[] trackIndices, "Tracks"))
        {
            foreach (var index in trackIndices)
            {
                if (index.TryLoad(out UObject track))
                    yield return track;
            }

            yield break;
        }

        if (binding.TryGetValue(out UObject[] trackObjects, "Tracks"))
        {
            foreach (var track in trackObjects)
            {
                if (track is not null)
                    yield return track;
            }
        }
    }

    private static IEnumerable<UObject> LoadSectionObjects(UObject track)
    {
        foreach (var propertyName in new[] { "AnimationSections", "Sections" })
        {
            if (track.TryGetValue(out FPackageIndex[] sectionIndices, propertyName))
            {
                foreach (var index in sectionIndices)
                {
                    if (index.TryLoad(out UObject section))
                        yield return section;
                }

                yield break;
            }

            if (track.TryGetValue(out UObject[] sectionObjects, propertyName))
            {
                foreach (var section in sectionObjects)
                {
                    if (section is not null)
                        yield return section;
                }

                yield break;
            }
        }
    }

    private static bool TryGetSectionAnimation(UObject section, out UAnimSequenceBase anim)
    {
        anim = null!;

        if (!section.TryGetValue(out FStructFallback parameters, "Params"))
            return false;

        if (parameters.TryGetValue(out UAnimSequenceBase directAnim, "Animation") && directAnim is not null)
        {
            anim = directAnim;
            return true;
        }

        if (parameters.TryGetValue(out FPackageIndex animIndex, "Animation")
            && animIndex.TryLoad(out UAnimSequenceBase indexedAnim))
        {
            anim = indexedAnim;
            return true;
        }

        if (parameters.TryGetValue(out FSoftObjectPath softAnim, "Animation")
            && !softAnim.AssetPathName.IsNone
            && UEParse.Provider.TryLoadPackageObject(softAnim.AssetPathName.Text, out var softObject)
            && softObject is UAnimSequenceBase softSequence)
        {
            anim = softSequence;
            return true;
        }

        return false;
    }

    private static string GetStringOrName(FStructFallback obj, string propertyName)
    {
        if (obj.TryGetValue(out FName name, propertyName) && !name.IsNone)
            return name.Text;
        return obj.GetOrDefault(propertyName, string.Empty);
    }
}
