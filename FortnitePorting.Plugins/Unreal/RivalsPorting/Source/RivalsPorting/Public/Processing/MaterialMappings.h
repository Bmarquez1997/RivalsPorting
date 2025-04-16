#pragma once
#include "RivalsPorting/Public/Utils.h"

struct FSlotMapping
{
	FString Name;
	FString Slot;
	FString SwitchSlot;
	
	FSlotMapping() { }
	
	FSlotMapping(const FString& Name, const FString& Slot = "", const FString& SwitchSlot = "")
	{
		this->Name = Name;
		this->Slot = Slot.IsEmpty() ? Name : Slot;
		this-> SwitchSlot = SwitchSlot;
	}
};

struct FMappingCollection
{
	TArray<FSlotMapping> Textures;
	TArray<FSlotMapping> Scalars;
	TArray<FSlotMapping> Vectors;
	TArray<FSlotMapping> Switches;
};

class FMaterialMappings
{
public:
	static const FMappingCollection Default;
	static const FMappingCollection Hero;
	static const FMappingCollection Hair;
	static const FMappingCollection Eye;
	static const FMappingCollection EyeGlass;
	static const FMappingCollection Translucent;
	static const FMappingCollection Layer;
};
