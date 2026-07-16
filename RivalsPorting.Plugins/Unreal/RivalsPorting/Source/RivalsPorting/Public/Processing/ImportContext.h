#pragma once
#include "EditorAssetLibrary.h"
#include "Factories/TextureFactory.h"
#include "Factories/UEFModelFactory.h"
#include "Utilities/JsonWrapper.h"
#include "World/BuildingActor.h"

class FImportContext
{
public:
	FImportContext(const FJsonWrapper& MetaData);
	void RunExport(const FJsonWrapper& Json);
	
	static void RunExportJson(const FString& Data);
	static void EnsureDependencies();
	
	inline static UMaterial* DefaultMaterial;
	inline static UMaterial* HeroMaterial;
	inline static UMaterial* HairMaterial;
	inline static UMaterial* EyeMaterial;
	inline static UMaterial* EyeGlassMaterial;
	inline static UMaterial* TranslucentMaterial;
	inline static UMaterial* RimMaterial;
	inline static UMaterial* LayerMaterial;
	
private:
	FJsonWrapper MetaData;
	
	void ImportMeshData(const FJsonWrapper& ExportData);
	void ImportTextureData(const FJsonWrapper& ExportData);
	
	
	UObject* ImportModel(const FJsonWrapper& ExportData, UWorld* World, ABuildingActor* Parent, const FJsonWrapper&
	                     MeshData, bool bCreateActor);
	UObject* ImportMesh(const FJsonWrapper& MeshData);
	
	UMaterialInstanceConstant* ImportMaterial(const FJsonWrapper& MaterialData);

	UBuildingTextureData* ImportBuildingTextureData(const FJsonWrapper& TexData);
	
	UTexture* ImportTexture(const FJsonWrapper& TextureData);
};
