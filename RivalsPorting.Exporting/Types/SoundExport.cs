using System.Collections.Generic;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Sound;
using CUE4Parse.UE4.Assets.Exports.Wwise;
using RivalsPorting.Exporting.Extensions;
using RivalsPorting.Exporting.Models;
using RivalsPorting.Exporting.Models.Files.Meta;

namespace RivalsPorting.Exporting.Types;

public class SoundExport : BaseExport
{
    public List<ExportSound> Sounds = [];
    
    public SoundExport(string name, UObject asset, EExportType exportType, ExportDataMeta metaData, IExportFileMeta? fileMeta) : base(name, exportType, metaData)
    {
        var exportSounds = new List<USoundWave>();
        var akAudioSounds = new List<string>();
        switch (asset)
        {
            case USoundWave soundWave:
            {
                exportSounds.Add(soundWave);
                break;
            }
            
            case USoundCue soundCue:
            {
                var sounds = soundCue.HandleSoundTree();
                foreach (var sound in sounds)
                {
                    var soundWave = sound.SoundWave.Load<USoundWave>();
                    if (soundWave is null) continue;
                    
                    exportSounds.Add(soundWave);
                }
                
                break;
            }

            case UAkAudioEvent akAudio:
            {
                akAudioSounds.AddRange(SoundExtensions.HandleSoundBnk(akAudio,
                    metaData.Provider.Provider,
                    metaData.Provider.ArchiveDirectory,
                    metaData.Provider.VgmStreamFile,
                    metaData.AssetsRoot,
                    metaData.CustomPath,
                    metaData.Settings.SoundFormat));
                break;
            }
            
            // TODO metasounds
        }
        
        foreach (var exportSound in exportSounds)
        {
            Sounds.Add(new ExportSound { Path = Context.Export(exportSound) });
        }

        foreach (var akTrack in akAudioSounds)
        {
            Sounds.Add(new ExportSound { Path = akTrack.Replace(metaData.AssetsRoot, "") });
        }
    }
    
}