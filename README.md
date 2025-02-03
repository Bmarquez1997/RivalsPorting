RivalsPorting - Automation of the Marvel Rivals Porting Process
------------------------------------------

#### Powered by [Avalonia UI](https://avaloniaui.net/) and [CUE4Parse](https://github.com/FabianFG/CUE4Parse)

[![Discord](https://discord.com/api/guilds/866821077769781249/widget.png?style=shield)](https://discord.gg/X7dzY6TUzw)
[![Blender](https://img.shields.io/badge/Blender-4.2+-blue?logo=blender&logoColor=white&color=orange)](https://www.blender.org/download/)
[![Unreal](https://img.shields.io/badge/Unreal-5.4+-blue?logo=unreal-engine&logoColor=white&color=white)](https://www.unrealengine.com/en-US/download)
[![Release](https://img.shields.io/github/release/bmarquez1997/RivalsPorting)]()
[![Downloads](https://img.shields.io/github/downloads/bmarquez1997/RivalsPorting/total?color=green)]()
***

![RivalsPorting_Preview](https://github.com/user-attachments/assets/b484bd1a-785b-4d83-ba35-93065888aafb)


## Building RivalsPorting

To build RivalsPorting from source, first clone the repository and all of its submodules.

```
git clone -b RivalsPorting https://github.com//bmarquez1997/RivalsPorting --recursive
```

Then open the project directory in a terminal window and publish

```
dotnet publish FortnitePorting -c Release --no-self-contained -r win-x64 -o "./Release" -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false -p:IncludeNativeLibrariesForSelfExtract=true
```
