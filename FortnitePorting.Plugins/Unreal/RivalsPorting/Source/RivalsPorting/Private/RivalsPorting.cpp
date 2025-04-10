// Copyright Epic Games, Inc. All Rights Reserved.

#define LOCTEXT_NAMESPACE "FRivalsPortingModule"
#include "RivalsPorting.h"

DEFINE_LOG_CATEGORY(LogRivalsPorting);

void FRivalsPortingModule::StartupModule()
{
	ListenServer.Start();
}

void FRivalsPortingModule::ShutdownModule()
{
	ListenServer.Shutdown();
}

#undef LOCTEXT_NAMESPACE
	
IMPLEMENT_MODULE(FRivalsPortingModule, RivalsPorting)