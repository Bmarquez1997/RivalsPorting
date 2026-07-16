#define LOCTEXT_NAMESPACE "FRivalsPortingModule"
#include "RivalsPorting.h"

#include "Classes/BuildingTextureData.h"
#include "Renderers/BuildingTextureDataThumbnailRenderer.h"
#include "ThumbnailRendering/ThumbnailManager.h"

DEFINE_LOG_CATEGORY(LogRivalsPorting);

void FRivalsPortingModule::StartupModule()
{
	ListenServer = new FListenServer();
	
	UThumbnailManager::Get().RegisterCustomRenderer(
		UBuildingTextureData::StaticClass(), 
		UBuildingTextureDataThumbnailRenderer::StaticClass()
	);
}

void FRivalsPortingModule::ShutdownModule()
{
	delete ListenServer;
}

#undef LOCTEXT_NAMESPACE
	
IMPLEMENT_MODULE(FRivalsPortingModule, RivalsPorting)