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
                var assetName = asset.Name;
                Dictionary<string, string> partMap = null;
                if (asset.TryGetValue(out UObject ams, "AssembledMeshSchema"))
                {
                    assetName = ams.Name;
                    if (ams.TryGetValue(out UObject coi, "CustomizableObjectInstance")
                        && coi.TryGetValue(out FStructFallback descriptor, "Descriptor")
                        && descriptor.TryGetValue(out FStructFallback[] intParams, "IntParameters"))
                    {
                        var paramName = intParams[0].Get<string>("ParameterName");
                        var paramValue = intParams[0].Get<string>("ParameterValueName");
                        partMap = intParams.ToDictionary(param => param.Get<string>("ParameterName"), 
                                                         param => param.Get<string>("ParameterValueName"));
                    }
                }

                String characterName = assetName.Substring(assetName.IndexOf("AMS_Figure_") + 11);

                AssetsVM.ExportChunks = 7;
                Meshes.AddIfNotNull(ExportLegoBody(characterName));
                Meshes.AddIfNotNull(ExportLegoHead(characterName));
                Meshes.AddIfNotNull(ExportLegoPart(characterName, "Piece"));
                Meshes.AddIfNotNull(ExportLegoPart(characterName, "HeadAcc"));
                Meshes.AddIfNotNull(ExportLegoPart(characterName, "NeckAcc"));
                Meshes.AddIfNotNull(ExportLegoPart(characterName, "HipAcc"));

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
                            ExportMesh exportMesh = new() { IsEmpty = true };
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
                    if (exportMesh is null) break;

                    if (asset.TryGetValue(out UMaterialInterface auxMaterial, "AuxiliaryMaterial"))
                        exportMesh?.Materials.AddIfNotNull(Exporter.Material(auxMaterial, 0));

                    if (asset.TryGetValue(out UMaterialInterface auxMaterial2, "AuxiliaryMaterial2"))
                        exportMesh?.Materials.AddIfNotNull(Exporter.Material(auxMaterial2, 1));

                    Meshes.AddIfNotNull(exportMesh);
                }


                break;
            }
            case EAssetType.FallGuysOutfit:
            {
                var parts = asset.GetOrDefault("BaseCharacterParts", Array.Empty<UObject>());
                foreach (var part in parts)
                {
                    Meshes.AddIfNotNull(Exporter.CharacterPart(part));
                }

                var parameterSet = new ExportOverrideParameters();
                parameterSet.MaterialNameToAlter = "Global";
                var additionalFields = asset.GetOrDefault("AdditionalDataFields", Array.Empty<FPackageIndex>());
                foreach (var additionalField in additionalFields)
                {
                    var field = additionalField.Load();
                    if (field is null) continue;
                    if (!field.ExportType.Equals("BeanCosmeticItemDefinitionBase")) continue;

                    void Texture(string propertyName, string shaderName)
                    {
                        if (!field.TryGetValue(out UTexture2D texture, propertyName)) return;

                        parameterSet.Textures.AddUnique(new TextureParameter(shaderName, 
                            Exporter.Export(texture), texture.SRGB, texture.CompressionSettings));
                    }

                    void ColorIndex(string propertyName, string shaderName)
                    {
                        if (!field.TryGetValue(out int index, propertyName)) return;

                        var color = CUE4ParseVM.BeanstalkColors[index];
                        parameterSet.Vectors.Add(new VectorParameter(shaderName, color.ToLinearColor()));
                    }

                    void MaterialTypeIndex(string propertyName, string shaderName)
                    {
                        if (!field.TryGetValue(out int index, propertyName)) return;

                        var color = CUE4ParseVM.BeanstalkMaterialProps[index];
                        parameterSet.Vectors.Add(new VectorParameter(shaderName, color));
                    }

                    void AtlasTextureSlotIndex(string propertyName, string shaderName)
                    {
                        if (!field.TryGetValue(out int index, propertyName))
                        {
                            parameterSet.Vectors.Add(new VectorParameter(shaderName, new FLinearColor(0, 0.5f, 0, 0)));
                            return;
                        }

                        var offset = CUE4ParseVM.BeanstalkAtlasTextureUVs[index];
                        parameterSet.Vectors.Add(new VectorParameter(shaderName, new FLinearColor(offset.X, offset.Y, offset.Z, 0)));
                    }

                    // Eye
                    ColorIndex("BodyEyesColorIndex", "Body_EyesColor");
                    MaterialTypeIndex("BodyEyesMaterialTypeIndex", "Body_Eyes_MaterialProps");

                    // Main
                    ColorIndex("BodyMainColorIndex", "Body_MainColor");
                    MaterialTypeIndex("BodyMainMaterialTypeIndex", "Body_MaterialProps");

                    // Pattern
                    Texture("Body_Pattern", "Body_Pattern");
                    ColorIndex("BodySecondaryColorIndex", "Body_SecondaryColor");
                    MaterialTypeIndex("BodySecondaryMaterialTypeIndex", "Body_Secondary_MaterialProps");

                    // Face Plate
                    ColorIndex("BodyFaceplateColorIndex", "Body_FacePlateColor");
                    MaterialTypeIndex("BodyFaceplateMaterialTypeIndex", "Body_Faceplate_MaterialProps");

                    // Face Items
                    ColorIndex("EyelashesColorIndex", "Eyelashes_Color");
                    MaterialTypeIndex("EyelashesMaterialTypeIndex", "Eyelashes_MaterialProps");
                    ColorIndex("GlassesFrameColorIndex", "Glasses_Frame_Color");
                    MaterialTypeIndex("GlassesFrameMaterialTypeIndex", "Glasses_Frame_MaterialProps");
                    ColorIndex("GlassesLensesColorIndex", "Glasses_Lense_Color");
                    MaterialTypeIndex("GlassesLensesMaterialTypeIndex", "Glasses_Lense_MaterialProps");

                    // Costume
                    ColorIndex("CostumeMainColorIndex", "Costume_MainColor");
                    MaterialTypeIndex("CostumeMainMaterialTypeIndex", "Costume_MainMaterialProps");
                    ColorIndex("CostumeSecondaryColorIndex", "Costume_Secondary_Color");
                    MaterialTypeIndex("CostumeSecondaryMaterialTypeIndex", "Costume_SecondaryMaterialProps");
                    ColorIndex("CostumeAccentColorIndex", "Costume_AccentColor");
                    MaterialTypeIndex("CostumeAccentMaterialTypeIndex", "Costume_AccentMaterialProps");
                    AtlasTextureSlotIndex("CostumePatternAtlasTextureSlot", "Costume_UVPatternPosition");


                    // Head Costume
                    ColorIndex("HeadCostumeMainColorIndex", "Head_Costume_MainColor");
                    MaterialTypeIndex("HeadCostumeMainMaterialTypeIndex", "Head_Costume_MainMaterialProps");
                    ColorIndex("HeadCostumeSecondaryColorIndex", "Head_Costume_Secondary_Color");
                    MaterialTypeIndex("HeadCostumeSecondaryMaterialTypeIndex", "Head_Costume_SecondaryMaterialProps");
                    ColorIndex("HeadCostumeAccentColorIndex", "Head_Costume_AccentColor");
                    MaterialTypeIndex("HeadCostumeAccentMaterialTypeIndex", "Head_Costume_AccentMaterialProps");
                    AtlasTextureSlotIndex("HeadCostumePatternAtlasTextureSlot", "Head_Costume_UVPatternPosition");

                    parameterSet.Vectors.Add(new VectorParameter("Body_GlassesEyeLashes", new FLinearColor
                    {
                        R = field.GetOrDefault<bool>("bGlasses") ? 1 : 0,
                        G = field.GetOrDefault<bool>("bGlassesLenses") ? 1 : 0,
                        B = field.GetOrDefault<bool>("bEyelashes") ? 1 : 0
                    }));
                }

                parameterSet.Hash = parameterSet.GetHashCode();

                OverrideParameters.Add(parameterSet);

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

    private static String FIGURE_CORE_PATH = "FortniteGame/Plugins/GameFeatures/Juno/FigureCharacter/Content/Figure_Core/";
    private static String FIGURE_COSMETICS_PATH = "FortniteGame/Plugins/GameFeatures/Juno/FigureCosmetics/Content/Figure/";

    private ExportMesh? ExportLegoHead(String characterName)
    {
        bool baseHead = false;
        bool headMeshHeadAccName = false;
        var mesh = ExportLegoPart(BuildPartFilePath(characterName, "Head"));
        if (mesh is null)
        {
            mesh = ExportLegoPart("_Figure_SharedParts/Head_" + characterName + "/SKM_Head_" + characterName + "_LP");
        }
        if (mesh is null)
        {
            mesh = ExportLegoPart("_Figure_SharedParts/Head_" + characterName + "/SKM_HeadAcc_" + characterName);
            headMeshHeadAccName = true;
        }
        if (mesh is null)
        {
            mesh = ExportLegoPart("_Figure_SharedParts/HeadAcc_3626/SKM_HeadAcc_3626");
            baseHead = true;
        }

        if (mesh is not null)
        {
            ExportMaterial headMaterial = null;
            if (CUE4ParseVM.Provider.TryLoadObject(
                    FIGURE_COSMETICS_PATH + "Figure_" + characterName + "/Material/MI_Figure_Head_" + characterName,
                    out UMaterialInstanceConstant material))
            {
                headMaterial = Exporter.Material(material, 0);
            }

            headMaterial ??= new ExportMaterial();
            headMaterial.Name = characterName + "_Head";
            headMaterial.Hash = headMaterial.Name.GetHashCode();
            headMaterial.Textures.AddRange(BuildPartTextureParameters(characterName, "Head", !baseHead));
            if (headMeshHeadAccName)
                headMaterial.Textures.AddRange(BuildPartTextureParameters(characterName, "HeadAcc", !baseHead));
            headMaterial.Textures.AddRange(BuildFaceTextureParameters(characterName));
            if (mesh.Materials.Count > 0)
                mesh.Materials[0] = headMaterial;
            else
                mesh.Materials.AddIfNotNull(headMaterial);
            mesh.CharacterPartType = EFortCustomPartType.Body;
        }
        return mesh;
    }

    private ExportMesh? ExportLegoBody(String characterName)
    {
        var mesh = ExportLegoPart("SkeletalMesh/SKM_Figure_Mutable", true);
        if (mesh is not null)
        {
            mesh.Name = "SKM_" + characterName;
            
            ExportMaterial bodyMaterial = mesh.Materials[0];
            bodyMaterial.Name = characterName + "_Body";
            bodyMaterial.Hash = bodyMaterial.Name.GetHashCode();
            bodyMaterial.Slot = 0;
            bodyMaterial.Textures.AddRange(BuildPartTextureParameters(characterName, "Body"));
            mesh.CharacterPartType = EFortCustomPartType.Body;
        }
        return mesh;
    }

    private ExportMesh? ExportLegoPart(String characterName, String partName)
    {
        var mesh = ExportLegoPart(BuildPartFilePath(characterName, partName));
        if (mesh is not null)
        {
            ExportMaterial partMaterial = null;
            if (CUE4ParseVM.Provider.TryLoadObject(
                    FIGURE_COSMETICS_PATH + "Figure_" + characterName + "/Material/MI_Figure_" + partName + "_" +
                    characterName, out UMaterialInstanceConstant material))
            {
                partMaterial = Exporter.Material(material, 0);
            }
            partMaterial ??= new ExportMaterial();
            partMaterial.Name = characterName + "_" + partName;
            partMaterial.Hash = partMaterial.Name.GetHashCode();
            partMaterial.Textures = BuildPartTextureParameters(characterName, partName);
            partMaterial.Slot = 0;
            if (mesh.Materials.Count > 0)
                mesh.Materials[0] = partMaterial;
            else
                mesh.Materials.AddIfNotNull(partMaterial);
            mesh.CharacterPartType = EFortCustomPartType.Body;
        }

        return mesh;
    }

    private ExportPart? ExportLegoPart(String filePath, bool corePath = false)
    {
        ExportPart exportMesh = null;
        if (CUE4ParseVM.Provider.TryLoadObject((corePath ? FIGURE_CORE_PATH : FIGURE_COSMETICS_PATH) + filePath, out UObject output))
        {
            exportMesh = Exporter.Mesh<ExportPart>(output as USkeletalMesh);
        }
        AssetsVM.ExportProgress++;
        return exportMesh;
    }

    private List<TextureParameter> BuildFaceTextureParameters(String characterName)
    {
        List<TextureParameter> textureParams = new();
        String facePath = FIGURE_CORE_PATH + "Texture/Face/";
        textureParams.AddIfNotNull(BuildTextureParameter(facePath + "Mouth/T_Atlas_Figure_Mouth_" + characterName, "Custom Mouth"));
        textureParams.AddIfNotNull(BuildTextureParameter(facePath + "Beards/T_Figure_Head_" + characterName + "_CharAcc", "Custom Head"));
        textureParams.AddIfNotNull(BuildTextureParameter(facePath + "AccentTeethTongue/T_Atlas_Figure_AccentTeethTongue_Default01", "AccentTeethTongue"));
        textureParams.AddIfNotNull(BuildTextureParameter(facePath + "Brow/T_Atlas_Figure_Brow_Char01", "Eyebrow_Char01"));
        textureParams.AddIfNotNull(BuildTextureParameter(facePath + "Brow/T_Atlas_Figure_Brow_Char02", "Eyebrow_Char02"));
        textureParams.AddIfNotNull(BuildTextureParameter(facePath + "Brow/T_Atlas_Figure_Brow_Char03", "Eyebrow_Char03"));
        textureParams.AddIfNotNull(BuildTextureParameter(facePath + "Brow/T_Atlas_Figure_Brow_Char04", "Eyebrow_Char04"));
        textureParams.AddIfNotNull(BuildTextureParameter(facePath + "Brow/T_Atlas_Figure_Brow_Thick01", "Eyebrow_Thick"));
        textureParams.AddIfNotNull(BuildTextureParameter(facePath + "Brow/T_Atlas_Figure_Brow_Thin01", "Eyebrow_Thin"));
        textureParams.AddIfNotNull(BuildTextureParameter(facePath + "EyeAndLash/TA_Atlas_Figure_EyeAndLash_Char01", "EyeAndLash_Char01"));
        textureParams.AddIfNotNull(BuildTextureParameter(facePath + "EyeAndLash/TA_Atlas_Figure_EyeAndLash_Char02", "EyeAndLash_Char02"));
        textureParams.AddIfNotNull(BuildTextureParameter(facePath + "EyeAndLash/TA_Atlas_Figure_EyeAndLash_Default01", "EyeAndLash_Default01"));
        textureParams.AddIfNotNull(BuildTextureParameter(facePath + "EyeAndLash/TA_Atlas_Figure_EyeAndLash_Large01", "EyeAndLash_Large01"));
        textureParams.AddIfNotNull(BuildTextureParameter(facePath + "T_Atlas_Figure_Faces", "Atlas Figure Faces"));
        return textureParams;
    }

    private List<TextureParameter> BuildPartTextureParameters(String characterName, String partName, bool includeNormals = true)
    {
        List<TextureParameter> textureParams = new();
        String path = FIGURE_COSMETICS_PATH + "Figure_" + characterName + "/Texture/T_Figure_" + partName + "_" + characterName + "_";
        textureParams.AddIfNotNull(BuildTextureParameter(path + "Elem_D", "Elem_D"));
        textureParams.AddIfNotNull(BuildTextureParameter(path + "Elem_M", "Elem_M"));
        textureParams.AddIfNotNull(BuildTextureParameter(path + "Deco_D", "Deco_D"));
        textureParams.AddIfNotNull(BuildTextureParameter(path + "Deco_M", "Deco_M"));
        textureParams.AddIfNotNull(BuildTextureParameter(path + "DecoBG_D", "DecoBG_D"));
        textureParams.AddIfNotNull(BuildTextureParameter(path + "DecoBG_M", "DecoBG_M"));
        textureParams.AddIfNotNull(BuildTextureParameter(path + "DecoFG_D", "DecoFG_D"));
        textureParams.AddIfNotNull(BuildTextureParameter(path + "DecoFG_M", "DecoFG_M"));
        if (includeNormals)
            textureParams.AddRange(BuildPartNormalTextureParameters(characterName, partName));
        return textureParams;
    }

    private List<TextureParameter> BuildPartNormalTextureParameters(string characterName, string part)
    {
        List<TextureParameter> textureParams = new List<TextureParameter>();
        foreach (var path in GetAccessoryNormalMaps(characterName))
        {
            if (path.Contains(part))
                textureParams.AddIfNotNull(BuildTextureParameter(path, "Normal"));
        }

        return textureParams;
    }

    private TextureParameter? BuildTextureParameter(string path, string name)
    {
        TextureParameter parameter = null;
        if (CUE4ParseVM.Provider.TryLoadObject(path, out UTexture2D textureAsset))
        {
            parameter = new(name, Exporter.Export(textureAsset), textureAsset.SRGB,
                textureAsset.CompressionSettings);
        }
        else if (CUE4ParseVM.Provider.TryLoadObject(path, out UTexture2DArray textureArrayAsset))
        {
            parameter = new(name, Exporter.Export(textureArrayAsset), textureArrayAsset.SRGB,
                textureArrayAsset.CompressionSettings);
        }
        return parameter;
    }
    
    private String BuildPartFilePath(String characterName, String partName)
    {
        return "_Figure_SharedParts/" + partName + "_" + characterName + "/SKM_" + partName + "_" + characterName;
    }

    private List<String> GetAccessoryNormalMaps(String characterName)
    {
        List<String> texturePaths = new List<string>();
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

        return texturePaths;
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
