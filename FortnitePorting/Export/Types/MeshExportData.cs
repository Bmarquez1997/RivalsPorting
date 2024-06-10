using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia.Media.TextFormatting.Unicode;
using CUE4Parse_Conversion.Meshes;
using CUE4Parse.GameTypes.FN.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Exports.Component.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.Component.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Exports.GeometryCollection;
using CUE4Parse.UE4.Assets.Exports.Material;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.GameplayTags;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.Utils;
using FortnitePorting.Application;
using FortnitePorting.Extensions;
using Serilog;

namespace FortnitePorting.Export.Types;

public class MeshExportData : ExportDataBase
{
    public readonly List<String> TexturePaths = [];
    public readonly List<ExportMesh> Meshes = [];
    public readonly List<ExportMesh> OverrideMeshes = [];
    public readonly List<ExportOverrideMaterial> OverrideMaterials = [];
    public readonly List<ExportOverrideParameters> OverrideParameters = [];
    public readonly AnimExportData Animation;

    public MeshExportData(string name, UObject asset, FStructFallback[] styles, EAssetType type, EExportTargetType exportType) : base(name, asset, styles, type, EExportType.Mesh, exportType)
    {
        switch (type)
        {
            case EAssetType.Outfit:
            {
                var parts = asset.GetOrDefault("BaseCharacterParts", Array.Empty<UObject>());
                if (asset.TryGetValue(out UObject heroDefinition, "HeroDefinition"))
                {
                    if (parts.Length == 0 &&
                        heroDefinition.TryGetValue(out UObject[] specializations, "Specializations"))
                    {
                        parts = specializations.First().GetOrDefault("CharacterParts", Array.Empty<UObject>());
                    }

                    if (Exporter.AppExportOptions.LobbyPoses &&
                        heroDefinition.TryGetValue(out UAnimMontage montage, "FrontendAnimMontageIdleOverride"))
                    {
                        Animation = AnimExportData.From(montage, exportType);
                    }
                }

                AssetsVM.ExportChunks = parts.Length;
                foreach (var part in parts)
                {
                    if (Exporter.AppExportOptions.LobbyPoses &&
                        part.TryGetValue(out UAnimMontage montage, "FrontendAnimMontageIdleOverride"))
                    {
                        Animation = AnimExportData.From(montage, exportType);
                    }

                    Meshes.AddIfNotNull(Exporter.CharacterPart(part));
                    AssetsVM.ExportProgress++;
                }

                if (Animation is null && Exporter.AppExportOptions.LobbyPoses &&
                    Meshes.FirstOrDefault(part => part is ExportPart
                    {
                        CharacterPartType: EFortCustomPartType.Body
                    }) is ExportPart foundPart)
                {
                    var montage = foundPart.GenderPermitted switch
                    {
                        EFortCustomGender.Male => CUE4ParseVM.MaleLobbyMontages.Random(),
                        EFortCustomGender.Female => CUE4ParseVM.FemaleLobbyMontages.Random(),
                    };

                    if (montage is not null) Animation = AnimExportData.From(montage, exportType);
                }

                break;
            }
            case EAssetType.LegoOutfit:
            {
                List<UObject> parts = new List<UObject>();
                List<UTexture2D> textures = new List<UTexture2D>();
                var assetName = asset.Name;
                if (asset.TryGetValue(out UObject ams, "AssembledMeshSchema"))
                {
                    assetName = ams.Name;
                }

                String characterName = assetName.Substring(assetName.IndexOf("AMS_Figure_") + 11);

                parts.AddIfNotNull(ExportLegoPart(BuildPartFilePath(characterName, "Head")));
                parts.AddIfNotNull(ExportLegoPart(BuildPartFilePath(characterName, "HeadAcc")));
                parts.AddIfNotNull(ExportLegoPart(BuildPartFilePath(characterName, "NeckAcc")));
                parts.AddIfNotNull(ExportLegoPart(BuildPartFilePath(characterName, "HipAcc")));
                parts.AddIfNotNull(ExportLegoPart("_Figure_Core/SkeletalMesh/SKM_Figure_PreviewA"));
                parts.AddIfNotNull(ExportLegoPart("_Figure_SharedParts/Head_" + characterName + "/SKM_HeadAcc_" +
                                                  characterName));

                textures = GetLegoTextures(characterName);


                // if (asset.TryGetValue(out UObject ams, "AssembledMeshSchema") && 
                //     ams.TryGetValue(out UObject mutable, "CustomizableObjectInstance") && 
                //     mutable.TryGetValue(out FInstancedStruct[] descriptors, "Descriptor"))
                // {
                //     foreach (var descriptor in descriptors)
                //     {
                //         if (descriptor.NonConstStruct?.TryGetValue(out UStruct[] intParams, "IntParameters") ?? false)
                //         {
                //             foreach (var intParameter in intParams)
                //             {
                //                 Log.Information(intParameter.ToString());
                //             }
                //         }
                //         
                //         /*
                //          * Head: Always search
                //          * Body: Standard
                //          * HipAcc: If not Debug, look for mesh
                //          * Textures: fuck if I know
                //          */
                //     }
                // }

                AssetsVM.ExportChunks = parts.Count() + textures.Count();
                foreach (var part in parts)
                {
                    ExportMesh mesh = Exporter.Mesh(part as USkeletalMesh);
                    // Build materials or override materials?
                    // Process each one during load, instead of looping through loaded props
                    Meshes.AddIfNotNull(mesh);
                    AssetsVM.ExportProgress++;
                }

                foreach (var texture in textures)
                {
                    // Move to asset loading above, only try to export when asset is loaded successfully
                    TexturePaths.AddIfNotNull(Exporter.Export(texture));
                    AssetsVM.ExportProgress++;
                }

                break;
            }
            case EAssetType.Backpack:
            {
                var parts = asset.GetOrDefault("CharacterParts", Array.Empty<UObject>());
                foreach (var part in parts) Meshes.AddIfNotNull(Exporter.CharacterPart(part));
                break;
            }
            case EAssetType.Pickaxe:
            {
                var weapon = asset.GetOrDefault<UObject?>("WeaponDefinition");
                if (weapon is null) break;

                Meshes.AddRange(Exporter.WeaponDefinition(weapon));
                break;
            }
            case EAssetType.Glider:
            {
                var mesh = asset.GetOrDefault<USkeletalMesh?>("SkeletalMesh");
                if (mesh is null) break;

                var part = Exporter.Mesh(mesh);
                if (part is null) break;

                var overrideMaterials = asset.GetOrDefault("MaterialOverrides", Array.Empty<FStructFallback>());
                foreach (var overrideMaterial in overrideMaterials)
                    part.OverrideMaterials.AddIfNotNull(Exporter.OverrideMaterial(overrideMaterial));

                Meshes.Add(part);
                break;
            }
            case EAssetType.Pet:
            {
                // backpack meshes
                var parts = asset.GetOrDefault("CharacterParts", Array.Empty<UObject>());
                foreach (var part in parts) Meshes.AddIfNotNull(Exporter.CharacterPart(part));

                // pet mesh
                var petAsset = asset.Get<UObject>("DefaultPet");
                var blueprintPath = petAsset.Get<FSoftObjectPath>("PetPrefabClass");
                var blueprintExports =
                    CUE4ParseVM.Provider.LoadAllObjects(blueprintPath.AssetPathName.Text.SubstringBeforeLast("."));
                var meshComponent =
                    blueprintExports.FirstOrDefault(export => export.Name.Equals("PetMesh0")) as
                        USkeletalMeshComponentBudgeted;
                var mesh = meshComponent?.GetSkeletalMesh().Load<USkeletalMesh>();
                if (mesh is not null) Meshes.AddIfNotNull(Exporter.Mesh(mesh));

                break;
            }
            case EAssetType.Toy:
            {
                var actor = asset.Get<UBlueprintGeneratedClass>("ToyActorClass");
                var parentActor = actor.SuperStruct.Load<UBlueprintGeneratedClass>();

                var exportComponent = GetComponent(actor);
                exportComponent ??= GetComponent(parentActor);
                if (exportComponent is null) break;

                Meshes.AddIfNotNull(Exporter.MeshComponent(exportComponent));
                break;

                UStaticMeshComponent? GetComponent(UBlueprintGeneratedClass? blueprint)
                {
                    if (blueprint is null) return null;
                    if (!blueprint.TryGetValue(out UObject internalComponentHandler, "InheritableComponentHandler"))
                        return null;

                    var records = internalComponentHandler.GetOrDefault("Records", Array.Empty<FStructFallback>());
                    foreach (var record in records)
                    {
                        var component = record.Get<UObject>("ComponentTemplate");
                        if (component is not UStaticMeshComponent staticMeshComponent) continue;

                        return staticMeshComponent;
                    }

                    return null;
                }
            }
            case EAssetType.Prop:
            {
                var levelSaveRecord = asset.Get<ULevelSaveRecord>("ActorSaveRecord");
                var meshes = Exporter.LevelSaveRecord(levelSaveRecord);
                Meshes.AddRange(meshes);
                break;
            }
            case EAssetType.Prefab:
            {
                if (asset.TryGetValue(out ULevelSaveRecord baseSaveRecord, "LevelSaveRecord"))
                {
                    Meshes.AddRange(Exporter.LevelSaveRecord(baseSaveRecord));
                }

                var recordCollectionLazy = asset.GetOrDefault<FPackageIndex?>("PlaysetPropLevelSaveRecordCollection");
                if (recordCollectionLazy is null || recordCollectionLazy.IsNull ||
                    !recordCollectionLazy.TryLoad(out var recordCollection) || recordCollection is null) break;

                var props = recordCollection.GetOrDefault<FStructFallback[]>("Items");
                AssetsVM.ExportChunks = props.Length;
                AssetsVM.ExportProgress = 0;
                foreach (var prop in props)
                {
                    var levelSaveRecord = prop.GetOrDefault<UObject?>("LevelSaveRecord");
                    if (levelSaveRecord is null) continue;

                    var actorSaveRecord = levelSaveRecord.Get<ULevelSaveRecord>("ActorSaveRecord");
                    var transform = prop.GetOrDefault<FTransform>("Transform");
                    var meshes = Exporter.LevelSaveRecord(actorSaveRecord);
                    foreach (var mesh in meshes)
                    {
                        mesh.Location += transform.Translation;
                        mesh.Rotation += transform.Rotator();
                        mesh.Scale *= transform.Scale3D;
                    }

                    Meshes.AddRange(meshes);
                    AssetsVM.ExportProgress++;
                }

                break;
            }
            case EAssetType.Item:
            {
                Meshes.AddRange(Exporter.WeaponDefinition(asset));
                break;
            }
            case EAssetType.Resource:
            {
                Meshes.AddRange(Exporter.WeaponDefinition(asset));
                break;
            }
            case EAssetType.Trap:
            {
                var actor = asset.Get<UBlueprintGeneratedClass>("BlueprintClass").ClassDefaultObject.Load();
                if (actor is null) break;

                var staticMesh = actor.GetOrDefault<UBaseBuildingStaticMeshComponent?>("StaticMeshComponent");
                if (staticMesh is not null)
                {
                    Meshes.AddIfNotNull(Exporter.MeshComponent(staticMesh));
                }

                var components = CUE4ParseVM.Provider.LoadAllObjects(actor.GetPathName().SubstringBeforeLast("."));
                foreach (var component in components)
                {
                    if (component.Name.Equals(staticMesh?.Name)) continue;
                    Meshes.AddIfNotNull(component switch
                    {
                        UStaticMeshComponent staticMeshComponent => Exporter.MeshComponent(staticMeshComponent),
                        USkeletalMeshComponent skeletalMeshComponent => Exporter.MeshComponent(skeletalMeshComponent),
                        _ => null
                    });
                }

                break;
            }
            case EAssetType.Vehicle:
            {
                var actor = asset.Get<UBlueprintGeneratedClass>("VehicleActorClass").ClassDefaultObject.Load();
                if (actor is null) break;

                var skeletalMesh = actor.GetOrDefault<UFortVehicleSkelMeshComponent?>("SkeletalMesh");
                if (skeletalMesh is not null)
                {
                    Meshes.AddIfNotNull(Exporter.MeshComponent(skeletalMesh));
                }

                var components = CUE4ParseVM.Provider.LoadAllObjects(actor.GetPathName().SubstringBeforeLast("."));
                foreach (var component in components)
                {
                    if (component.Name.Equals(skeletalMesh?.Name)) continue;
                    Meshes.AddIfNotNull(component switch
                    {
                        UStaticMeshComponent staticMeshComponent => Exporter.MeshComponent(staticMeshComponent),
                        _ => null
                    });
                }

                break;
            }
            //case EAssetType.LegoWildlife:
                // if (asset.TryGetValue(out USkeletalMesh legoSkelMesh, "SkeletalMesh"))
                // {
                //     var exportMesh = Exporter.Mesh(legoSkelMesh);
                //     if (exportMesh is not null && asset.TryGetValue(out FStructFallback[] overrideMats, "OverrideMaterials"))
                //     {
                //         foreach (var mat in overrideMats)
                //         {
                //             if (mat.TryGetValue(out UMaterialInterface overrideMat, "Material"))
                //                 exportMesh.OverrideMaterials.AddIfNotNull(Exporter.Material(overrideMat, 0));
                //         }
                //     }
                
                    
                    //Meshes.AddIfNotNull(exportMesh);
                //}
                //break;
            case EAssetType.Wildlife:
            {
                var wildlifeMesh = (USkeletalMesh) asset;
                Meshes.AddIfNotNull(Exporter.Mesh(wildlifeMesh));
                break;
            }
            case EAssetType.LegoProp:
                var blueprintObject = asset.Get<UBlueprintGeneratedClass>("ActorClassToBuild");
                if (blueprintObject is null) break;

                var length = 0;
                FPackageIndex[] allNodes = [];
                IPropertyHolder[] records = [];
                if (blueprintObject.TryGetValue(out FPackageIndex simpleConstructionScript,
                        "SimpleConstructionScript") &&
                    simpleConstructionScript.TryLoad(out var scs) && scs.TryGetValue(out allNodes, "AllNodes"))
                {
                    length = allNodes.Length;
                }
                else if (blueprintObject.TryGetValue(out FPackageIndex inheritableComponentHandler,
                             "InheritableComponentHandler") &&
                         inheritableComponentHandler.TryLoad(out var ich) && ich.TryGetValue(out records, "Records"))
                {
                    length = records.Length;
                }
                
                AssetsVM.ExportChunks = length;
                AssetsVM.ExportProgress = 0;
                var exportMeshes = new List<ExportMesh>();
                for (var i = 0; i < length; i++)
                {
                    IPropertyHolder actor;
                    if (allNodes is {Length: > 0} && allNodes[i].TryLoad(out UObject node))
                    {
                        actor = node;
                    }
                    else if (records is {Length: > 0})
                    {
                        actor = records[i];
                    }
                    else continue;

                    if (actor.TryGetValue(out FPackageIndex componentTemplate, "ComponentTemplate") &&
                        componentTemplate.TryLoad(out UObject compTemplate))
                    {
                        UGeometryCollection geometryCollection = null;
                        if (!compTemplate.TryGetValue(out UStaticMesh m, "StaticMesh") &&
                            compTemplate.TryGetValue(out FPackageIndex restCollection, "RestCollection") &&
                            restCollection.TryLoad(out geometryCollection))
                        {
                            if (geometryCollection.RootProxyData is { ProxyMeshes.Length: > 0 } rootProxyData)
                            {
                                rootProxyData.ProxyMeshes[0].TryLoad(out m);
                            }
                        }

                        if (m is { Materials.Length: > 0 })
                        {
                            // Uncomment to change vertex color directly instead of using LUT Material in blender
                            // OverrideJunoVertexColors(m, geometryCollection);
                            ExportMesh exportMesh = new ExportMesh { IsEmpty = true };
                            if (componentTemplate.TryLoad(out UStaticMeshComponent meshComp))
                            {
                                Log.Information(compTemplate.ToString());
                                exportMesh = Exporter.MeshComponent(meshComp) ?? new ExportMesh { IsEmpty = true };
                                exportMesh.Name = m.Name;
                                exportMesh.Location = meshComp.GetOrDefault("RelativeLocation", FVector.ZeroVector);
                                exportMesh.Rotation = meshComp.GetOrDefault("RelativeRotation", FRotator.ZeroRotator);
                                exportMesh.Scale = meshComp.GetOrDefault("RelativeScale3D", FVector.OneVector);
                            }
                            else
                            {
                                exportMesh = Exporter.Mesh(m);
                            }

                            exportMeshes.AddIfNotNull(exportMesh);
                            //ProcessMesh(actor, compTemplate, m, CalculateTransform(compTemplate, transform), forceShow);
                        }
                    }
                    else if (actor.TryGetValue(out FPackageIndex staticMeshComponent, "StaticMeshComponent", "ComponentTemplate", "StaticMesh", "Mesh", "LightMesh") &&
                             staticMeshComponent.TryLoad(out UStaticMeshComponent staticMeshComp) &&
                             staticMeshComp.GetStaticMesh().TryLoad(out UStaticMesh m) && m.Materials.Length > 0)
                    {
                        Log.Information(staticMeshComp.ToString());
                        var exportMesh = Exporter.MeshComponent(staticMeshComp) ?? new ExportMesh { IsEmpty = true };
                        exportMesh.Name = m.Name;
                        exportMesh.Location = staticMeshComp.GetOrDefault("RelativeLocation", FVector.ZeroVector);
                        exportMesh.Rotation = staticMeshComp.GetOrDefault("RelativeRotation", FRotator.ZeroRotator);
                        exportMesh.Scale = staticMeshComp.GetOrDefault("RelativeScale3D", FVector.OneVector);
                        exportMeshes.Add(exportMesh);
                        //ProcessMesh(actor, staticMeshComp, m, CalculateTransform(staticMeshComp, transform));
                    }
                    else if (actor.TryGetValue(out FPackageIndex staticMeshComponent2, "StaticMeshComponent",
                                 "ComponentTemplate", "StaticMesh", "Mesh", "LightMesh") &&
                             staticMeshComponent2.TryLoad(out UStaticMeshComponent staticMeshComp2) &&
                             staticMeshComp2.GetStaticMesh().TryLoad(out UStaticMesh m2))
                    {
                        Log.Information("Issue with material count");
                    }
                    else if (actor.TryGetValue(out FPackageIndex staticMeshComponent3, "StaticMeshComponent",
                                 "ComponentTemplate", "StaticMesh", "Mesh", "LightMesh") &&
                             staticMeshComponent3.TryLoad(out UStaticMeshComponent staticMeshComp3))
                    {
                        Log.Information("Issue with UStaticMesh load");
                    }
                    else if (actor.TryGetValue(out FPackageIndex staticMeshComponent4, "StaticMeshComponent",
                                 "ComponentTemplate", "StaticMesh", "Mesh", "LightMesh"))
                    {
                        Log.Information("Issue with UStaticMeshComponent load");
                    }

                    AssetsVM.ExportProgress++;
                }
                Meshes.AddRange(exportMeshes);
                break;
            case EAssetType.WeaponMod:
            case EAssetType.Mesh:
            {
                switch (asset)
                {
                    case USkeletalMesh skeletalMesh:
                        Meshes.AddIfNotNull(Exporter.Mesh(skeletalMesh));
                        break;
                    case UStaticMesh staticMesh:
                        Meshes.AddIfNotNull(Exporter.Mesh(staticMesh));
                        break;
                }

                break;
            }
            case EAssetType.World:
            {
                if (asset is not UWorld world) return;
                Meshes.AddRange(ProcessWorld(world));
                
                break;

                IEnumerable<ExportMesh> ProcessWorld(UWorld world)
                {
                    if (world.PersistentLevel.Load() is not ULevel level) return Enumerable.Empty<ExportMesh>();

                    FilesVM.ExportChunks += level.Actors.Length;

                    var actors = new List<ExportMesh>();
                    foreach (var actorLazy in level.Actors)
                    {
                        FilesVM.ExportProgress++;
                        if (actorLazy is null || actorLazy.IsNull) continue;

                        var actor = actorLazy.Load();
                        if (actor is null) continue;
                        if (actor.ExportType == "LODActor") continue;

                        Log.Information("Processing {0}: {1}/{2}", actor.Name, FilesVM.ExportProgress, FilesVM.ExportChunks);
                        actors.AddIfNotNull(ProcessActor(actor));
                    }

                    return actors;
                }

                ExportMesh? ProcessActor(UObject actor)
                {
                    
                    if (actor.TryGetValue(out UStaticMeshComponent staticMeshComponent, "StaticMeshComponent", "StaticMesh", "Mesh", "LightMesh"))
                    {
                        var exportMesh = Exporter.MeshComponent(staticMeshComponent) ?? new ExportMesh { IsEmpty = true };
                        exportMesh.Name = actor.Name;
                        exportMesh.Location = staticMeshComponent.GetOrDefault("RelativeLocation", FVector.ZeroVector);
                        exportMesh.Rotation = staticMeshComponent.GetOrDefault("RelativeRotation", FRotator.ZeroRotator);
                        exportMesh.Scale = staticMeshComponent.GetOrDefault("RelativeScale3D", FVector.OneVector);

                        foreach (var extraMesh in Exporter.ExtraActorMeshes(actor))
                        {
                            exportMesh.Children.AddIfNotNull(extraMesh);
                        }

                        var textureDatas = actor.GetAllProperties<UBuildingTextureData>("TextureData");
                        if (textureDatas.Count == 0 && actor.Template is not null)
                            textureDatas = actor.Template.Load()!.GetAllProperties<UBuildingTextureData>("TextureData");

                        foreach (var (textureData, index) in textureDatas)
                        {
                            exportMesh.TextureData.AddIfNotNull(Exporter.TextureData(textureData, index));
                        }
                        
                        if (actor.TryGetValue(out FSoftObjectPath[] additionalWorlds, "AdditionalWorlds"))
                        {
                            foreach (var additionalWorldPath in additionalWorlds)
                            {
                                exportMesh.Children.AddRange(ProcessWorld(additionalWorldPath.Load<UWorld>()));
                            }
                        }

                        return exportMesh;
                    }

                    return null;
                }
            }
            case EAssetType.FestivalBass:
            case EAssetType.FestivalDrum:
            case EAssetType.FestivalGuitar:
            case EAssetType.FestivalKeytar:
            case EAssetType.FestivalMic:
            {
                if (asset.TryGetValue(out USkeletalMesh mesh, "Mesh"))
                {
                    var exportMesh = Exporter.Mesh(mesh);

                    var material = asset.GetOrDefault<UMaterialInterface>("Material");
                    exportMesh?.Materials.AddIfNotNull(Exporter.Material(material, 0));

                    Meshes.AddIfNotNull(exportMesh);
                }

                if (asset.TryGetValue(out USkeletalMesh leftHandMesh, "LeftHandMesh"))
                {
                    var exportMesh = Exporter.Mesh(leftHandMesh);

                    var material = asset.GetOrDefault<UMaterialInterface>("LeftHandMaterial");
                    exportMesh?.Materials.AddIfNotNull(Exporter.Material(material, 0));

                    Meshes.AddIfNotNull(exportMesh);
                }

                if (asset.TryGetValue(out USkeletalMesh auxiliaryMesh, "AuxiliaryMesh"))
                {
                    var exportMesh = Exporter.Mesh(auxiliaryMesh);

                    var auxMaterial = asset.GetOrDefault<UMaterialInterface>("AuxiliaryMaterial");
                    exportMesh?.Materials.AddIfNotNull(Exporter.Material(auxMaterial, 0));

                    var auxMaterial2 = asset.GetOrDefault<UMaterialInterface>("AuxiliaryMaterial2");
                    exportMesh?.Materials.AddIfNotNull(Exporter.Material(auxMaterial2, 1));

                    Meshes.AddIfNotNull(exportMesh);
                }


                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }

        ExportStyles(asset, styles);
    }

    private void ExportStyles(UObject asset, FStructFallback[] styles)
    {
        var metaTagsToApply = new List<FGameplayTag>();
        var metaTagsToRemove = new List<FGameplayTag>();
        foreach (var style in styles)
        {
            var tags = style.Get<FStructFallback>("MetaTags");

            var tagsToApply = tags.Get<FGameplayTagContainer>("MetaTagsToApply");
            metaTagsToApply.AddRange(tagsToApply.GameplayTags);

            var tagsToRemove = tags.Get<FGameplayTagContainer>("MetaTagsToRemove");
            metaTagsToRemove.AddRange(tagsToRemove.GameplayTags);
        }

        var metaTags = new FGameplayTagContainer(metaTagsToApply.Where(tag => !metaTagsToRemove.Contains(tag)).ToArray());
        var itemStyles = asset.GetOrDefault("ItemVariants", Array.Empty<UObject>());
        var tagDrivenStyles = itemStyles.Where(style => style.ExportType.Equals("FortCosmeticLoadoutTagDrivenVariant"));
        foreach (var tagDrivenStyle in tagDrivenStyles)
        {
            var options = tagDrivenStyle.Get<FStructFallback[]>("Variants");
            foreach (var option in options)
            {
                var requiredConditions = option.Get<FStructFallback[]>("RequiredConditions");
                foreach (var condition in requiredConditions)
                {
                    var metaTagQuery = condition.Get<FGameplayTagQuery>("MetaTagQuery");
                    if (metaTags.MatchesQuery(metaTagQuery)) ExportStyleData(option);
                }
            }
        }

        foreach (var style in styles) ExportStyleData(style);
    }

    private void ExportStyleData(FStructFallback style)
    {
        var variantParts = style.GetOrDefault("VariantParts", Array.Empty<UObject>());
        foreach (var part in variantParts) OverrideMeshes.AddIfNotNull(Exporter.CharacterPart(part));

        var variantMaterials = style.GetOrDefault("VariantMaterials", Array.Empty<FStructFallback>());
        foreach (var material in variantMaterials) OverrideMaterials.AddIfNotNull(Exporter.OverrideMaterial(material));

        var variantParameters = style.GetOrDefault("VariantMaterialParams", Array.Empty<FStructFallback>());
        foreach (var parameters in variantParameters) OverrideParameters.AddIfNotNull(Exporter.OverrideParameters(parameters));
    }

    private String BuildPartFilePath(String characterName, String partName)
    {
        return "_Figure_SharedParts/" + partName + "_" + characterName + "/SKM_" + partName + "_" + characterName;
    }

    private UObject ExportLegoPart(String filePath)
    {
        CUE4ParseVM.Provider.TryLoadObject(
            "FortniteGame/Plugins/GameFeatures/Juno/FigureCosmetics/Content/Figure/" + filePath,
            out UObject output);

        return output;
    }
    
    private static String FIGURE_COSMETICS_PATH = "FortniteGame/Plugins/GameFeatures/Juno/FigureCosmetics/Content/Figure/";

    private List<UTexture2D> GetLegoTextures(String characterName)
    {
        List<UTexture2D> textures = new List<UTexture2D>();
        foreach (var texturePath in BuildLegoTexturePaths(characterName))
        {
            Log.Information("Loading Texture: " + texturePath);
            if (CUE4ParseVM.Provider.TryLoadObject(texturePath, out UTexture2D textureAsset))
            {
                Log.Information("Loaded!");
                textures.Add(textureAsset);
            }
        }
        
        return textures;
    }
    
    private List<String> BuildLegoTexturePaths(String characterName)
    {
        // TODO: Create override materials and add to ExportMesh objects instead of just importing to file
        List<String> textureNames = new List<string>();

        AddFaceTextures(textureNames, characterName);
        AddAccessoryNormalMaps(textureNames, characterName);
        AddLegoTextureLayerPaths(textureNames, "Figure_" + characterName + "/Texture/T_Figure_Body_" + characterName);
        AddLegoTextureLayerPaths(textureNames, "Figure_" + characterName + "/Texture/T_Figure_Head_" + characterName);
        AddLegoTextureLayerPaths(textureNames, "Figure_" + characterName + "/Texture/T_Figure_HeadFront_" + characterName);
        AddLegoTextureLayerPaths(textureNames, "Figure_" + characterName + "/Texture/T_Figure_HeadBack_" + characterName);
        AddLegoTextureLayerPaths(textureNames, "Figure_" + characterName + "/Texture/T_Figure_HeadAcc_" + characterName);
        AddLegoTextureLayerPaths(textureNames, "Figure_" + characterName + "/Texture/T_Figure_HipAcc_" + characterName);

        return textureNames;
    }

    private void AddFaceTextures(List<String> texturePaths, String characterName)
    {
        String facePath = "FortniteGame/Plugins/GameFeatures/Juno/FigureCharacter/Content/Figure_Core/Texture/Face/";
        texturePaths.Add(facePath + "Mouth/T_Atlas_Figure_Mouth_" + characterName);
        texturePaths.Add(facePath + "Beards/T_Figure_Head_" + characterName + "_CharAcc");
        texturePaths.Add(facePath + "AccentTeethTongue/T_Atlas_Figure_AccentTeethTongue_Default01");
        texturePaths.Add(facePath + "Brow/T_Atlas_Figure_Brow_Char01");
        texturePaths.Add(facePath + "Brow/T_Atlas_Figure_Brow_Char02");
        texturePaths.Add(facePath + "Brow/T_Atlas_Figure_Brow_Char03");
        texturePaths.Add(facePath + "Brow/T_Atlas_Figure_Brow_Char04");
        texturePaths.Add(facePath + "Brow/T_Atlas_Figure_Brow_Thick01");
        texturePaths.Add(facePath + "Brow/T_Atlas_Figure_Brow_Thin01");
        texturePaths.Add(facePath + "EyeAndLash/TA_Atlas_Figure_EyeAndLash_Char01");
        texturePaths.Add(facePath + "EyeAndLash/TA_Atlas_Figure_EyeAndLash_Default01");
        texturePaths.Add(facePath + "EyeAndLash/TA_Atlas_Figure_EyeAndLash_Large01");
        texturePaths.Add(facePath + "T_Atlas_Figure_Faces");
    }

    private void AddAccessoryNormalMaps(List<String> texturePaths, String characterName)
    {
        texturePaths.Add(FIGURE_COSMETICS_PATH + "_Figure_SharedParts/Head_" + characterName + "/T_Figure_Head_" + characterName + "_N");
        texturePaths.Add(FIGURE_COSMETICS_PATH + "_Figure_SharedParts/Head_" + characterName + "/T_Figure_HeadAcc_" + characterName + "_N");
        texturePaths.Add(FIGURE_COSMETICS_PATH + "_Figure_SharedParts/HeadAcc_" + characterName + "/T_Figure_HeadAcc_" + characterName + "_N");
        texturePaths.Add(FIGURE_COSMETICS_PATH + "_Figure_SharedParts/HipAcc_" + characterName + "/T_Figure_HipAcc_" + characterName + "_N");
        texturePaths.Add(FIGURE_COSMETICS_PATH + "_Figure_SharedParts/HipAcc_" + characterName + "/T_Figure_NeckAcc_" + characterName + "_N");
        texturePaths.Add(FIGURE_COSMETICS_PATH + "_Figure_SharedParts/Head_" + characterName + "/T_Head_" + characterName + "_N");
        texturePaths.Add(FIGURE_COSMETICS_PATH + "_Figure_SharedParts/Head_" + characterName + "/T_HeadAcc_" + characterName + "_N");
        texturePaths.Add(FIGURE_COSMETICS_PATH + "_Figure_SharedParts/HeadAcc_" + characterName + "/T_HeadAcc_" + characterName + "_N");
        texturePaths.Add(FIGURE_COSMETICS_PATH + "_Figure_SharedParts/HipAcc_" + characterName + "/T_HipAcc_" + characterName + "_N");
        texturePaths.Add(FIGURE_COSMETICS_PATH + "_Figure_SharedParts/HipAcc_" + characterName + "/T_NeckAcc_" + characterName + "_N");
    }

    private void AddLegoTextureLayerPaths(List<String> texturePaths, String basePath)
    {
        String path = FIGURE_COSMETICS_PATH + basePath;
        texturePaths.Add(path + "_Elem_D");
        texturePaths.Add(path + "_Elem_M");
        texturePaths.Add(path + "_Deco_D");
        texturePaths.Add(path + "_Deco_M");
        texturePaths.Add(path + "_DecoBG_D");
        texturePaths.Add(path + "_DecoBG_M");
        texturePaths.Add(path + "_DecoFG_D");
        texturePaths.Add(path + "_DecoFG_M");
    }
    
    // TODO: Take override LUT textures into account, parse images instead of using default material definitions via ID
    private void OverrideJunoVertexColors(UStaticMesh staticMesh, UGeometryCollection geometryCollection = null)
    {
        if (staticMesh.RenderData is not { LODs.Length: > 0 } || staticMesh.RenderData.LODs[0].ColorVertexBuffer == null)
            return;

        var dico = new Dictionary<byte, FColor>();
        if (geometryCollection?.Materials is not { Length: > 0 })
        {
            var distinctReds = new HashSet<byte>();
            for (int i = 0; i < staticMesh.RenderData.LODs[0].ColorVertexBuffer.Data.Length; i++)
            {
                ref var vertexColor = ref staticMesh.RenderData.LODs[0].ColorVertexBuffer.Data[i];
                var indexAsByte = vertexColor.R;
                if (vertexColor.R == 255) indexAsByte = vertexColor.A;
                distinctReds.Add(indexAsByte);
            }

            foreach (var indexAsByte in distinctReds)
            {
                var path = string.Concat("/JunoAtomAssets/Materials/MI_LegoStandard_", indexAsByte, ".MI_LegoStandard_", indexAsByte);
                if (!CUE4ParseVM.Provider.TryLoadObject(path, out UMaterialInterface unrealMaterial))
                    continue;

                var parameters = new CMaterialParams2();
                unrealMaterial.GetParams(parameters, EMaterialFormat.FirstLayer);

                if (!parameters.TryGetLinearColor(out var color, "Color"))
                    color = FLinearColor.Gray;

                dico[indexAsByte] = color.ToFColor(true);
            }
        }
        else foreach (var material in geometryCollection.Materials)
        {
            if (!material.TryLoad(out UMaterialInterface unrealMaterial))
                continue;

            var parameters = new CMaterialParams2();
            unrealMaterial.GetParams(parameters, EMaterialFormat.FirstLayer);

            if (!byte.TryParse(material.Name.SubstringAfterLast("_"), out var indexAsByte))
                indexAsByte = byte.MaxValue;
            if (!parameters.TryGetLinearColor(out var color, "Color"))
                color = FLinearColor.Gray;

            dico[indexAsByte] = color.ToFColor(true);
        }

        for (int i = 0; i < staticMesh.RenderData.LODs[0].ColorVertexBuffer.Data.Length; i++)
        {
            ref var vertexColor = ref staticMesh.RenderData.LODs[0].ColorVertexBuffer.Data[i];
            vertexColor = dico.TryGetValue(vertexColor.R, out var color) ? color : FColor.Gray;
        }
    }
}