#pragma once
#include "HttpServerModule.h"

class FListenServer
{
public:
	int Port = 20026;
	FString DataPath = TEXT("/Rivals-porting/data");
	FString PingPath = TEXT("/Rivals-porting/ping");
	
	FHttpServerModule& Module = FHttpServerModule::Get();
	
	void Start() const;
	void Shutdown() const;
};
