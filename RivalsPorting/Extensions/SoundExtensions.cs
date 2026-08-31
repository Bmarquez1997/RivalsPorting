using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CUE4Parse_Conversion.Sounds;
using CUE4Parse.GameTypes.FN.Assets.Exports.Sound;
using CUE4Parse.UE4.Assets.Exports.Sound;
using CUE4Parse.UE4.Assets.Exports.Sound.Node;
using CUE4Parse.UE4.Assets.Exports.Wwise;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Wwise;
using CUE4Parse.Utils;
using RivalsPorting.CUE4Parse.Extensions;
using RivalsPorting.Shared.Extensions;
using Log = Serilog.Log;

namespace RivalsPorting.Exporting.Extensions;

public static class SoundExtensions
{
    public static bool TrySaveSoundToPath(USoundWave soundWave, string path,
        FileInfo? binkaDecoder = null, FileInfo? radaDecoder = null, FileInfo? vgmStream = null)
    {
        soundWave.Decode(true, out var format, out var data);
        if (data is null) soundWave.Decode(false, out format, out data);
        if (data is null) return false;

        switch (format.ToLower())
        {
            case "adpcm":
                SaveADPCMAsWav(data, path, vgmStream);
                break;
            case "binka":
                SaveBinkaAsWav(data, path, binkaDecoder);
                break;
            case "rada":
                SaveRadaAsWav(data, path, radaDecoder);
                break;
        }

        return true;
    }

    public static bool TrySaveSoundToPath(USoundWave soundWave, string path, out Stream stream,
        FileInfo? binkaDecoder = null, FileInfo? radaDecoder = null, FileInfo? vgmStream = null)
    {
        if (File.Exists(path) || TrySaveSoundToPath(soundWave, path, binkaDecoder, radaDecoder, vgmStream))
        {
            stream = new FileStream(path, FileMode.Open, FileAccess.Read);
            return true;
        }

        stream = null;
        return false;
    }

    public static bool TrySaveSoundToAssets(USoundWave soundWave, string assetsRoot, out string path,
        FileInfo? binkaDecoder = null, FileInfo? radaDecoder = null, FileInfo? vgmStream = null)
    {
        path = Path.Combine(assetsRoot, soundWave.GetCleanedExportPath() + ".wav");
        Directory.CreateDirectory(path.SubstringBeforeLast("/"));

        if (File.Exists(path) || TrySaveSoundToPath(soundWave, path, binkaDecoder, radaDecoder, vgmStream))
        {
            return true;
        }

        return false;
    }

    public static bool TrySaveSoundToAssets(USoundWave soundWave, string assetsRoot, out Stream stream,
        FileInfo? binkaDecoder = null, FileInfo? radaDecoder = null, FileInfo? vgmStream = null)
    {
        var path = Path.Combine(assetsRoot, soundWave.GetCleanedExportPath() + ".wav");
        Directory.CreateDirectory(path.SubstringBeforeLast("/"));

        if (File.Exists(path) || TrySaveSoundToPath(soundWave, path, binkaDecoder, radaDecoder, vgmStream))
        {
            stream = new FileStream(path, FileMode.Open, FileAccess.Read);
            return true;
        }

        stream = null;
        return false;
    }

    public static void SaveBinkaAsWav(byte[] data, string outPath, FileInfo? binkaDecoder = null)
    {
        if (binkaDecoder is null) return;
        var binkaPath = Path.ChangeExtension(outPath, "binka");
        File.WriteAllBytes(binkaPath, data);

        using (var binkaProcess = new Process())
        {
            binkaProcess.StartInfo = new ProcessStartInfo
            {
                FileName = binkaDecoder.FullName,
                Arguments = $"-i \"{binkaPath}\" -o \"{outPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            binkaProcess.Start();
            binkaProcess.WaitForExit();
        }

        FileSystemExtensions.TryDeleteFile(binkaPath);
    }

    public static void SaveRadaAsWav(byte[] data, string outPath, FileInfo? radaDecoder = null)
    {
        if (radaDecoder is null) return;
        var radaPath = Path.ChangeExtension(outPath, "rada");
        File.WriteAllBytes(radaPath, data);

        using (var radaProcess = new Process())
        {
            radaProcess.StartInfo = new ProcessStartInfo
            {
                FileName = radaDecoder.FullName,
                Arguments = $"-i \"{radaPath}\" -o \"{outPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };

            radaProcess.Start();
            radaProcess.WaitForExit();
        }

        FileSystemExtensions.TryDeleteFile(radaPath);
    }

    public static void SaveADPCMAsWav(byte[] data, string outPath, FileInfo? vgmStream = null)
    {
        if (vgmStream is null) return;
        var adpcmPath = Path.ChangeExtension(outPath, "adpcm");
        File.WriteAllBytes(adpcmPath, data);

        using (var adpcmProcess = new Process())
        {
            adpcmProcess.StartInfo = new ProcessStartInfo
            {
                FileName = vgmStream.FullName,
                Arguments = $"-o \"{outPath}\" \"{adpcmPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            adpcmProcess.Start();
            adpcmProcess.WaitForExit();
        }

        FileSystemExtensions.TryDeleteFile(adpcmPath);
    }

    public static List<string> HandleSoundBnk(UAkAudioEvent akAudio, string assetsRoot, string? customPath, ESoundFormat soundFormat = ESoundFormat.WAV)
    {
        var trackPaths = new List<string>();
        var wwiseProvider = new WwiseProvider(UEParse.Provider, AppSettings.Installation.CurrentProfile.ArchiveDirectory);
        var events = wwiseProvider.ExtractAudioEventSounds(akAudio);

        foreach (var audioEvent in events)
        {
            var namedPath = audioEvent.ToString().Replace("/Content/", "/");
            var rootPath = customPath ?? assetsRoot;

            var savedAudioPath = Path.Combine(rootPath, customPath == null ? $"Game/{namedPath}" : namedPath.SubstringAfterLast('/'));

            if (TrySaveBnkTrack(savedAudioPath, audioEvent.Data?.GetData(), out var wavPath, GetNewFileExtension(soundFormat)))
                trackPaths.Add(wavPath);
        }

        if (trackPaths.Count == 4) trackPaths.RemoveAt(0);
        return trackPaths;
    }

    public static bool TrySaveBnkTrack(string inputFilePath, byte[]? inputFileData, out string wavFilePath, string fileExtension = ".wav")
    {
        wavFilePath = string.Empty;
        if (inputFileData == null) return false;

        var vgmFilePath = Dependencies.VgmStreamFile.FullName;

        Directory.CreateDirectory(inputFilePath.SubstringBeforeLast("/"));
        File.WriteAllBytes(inputFilePath, inputFileData);

        wavFilePath = Path.ChangeExtension(inputFilePath, fileExtension);
        var vgmProcess = Process.Start(new ProcessStartInfo
        {
            FileName = vgmFilePath,
            Arguments = $"-o \"{wavFilePath}\" \"{inputFilePath}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        });
        vgmProcess?.WaitForExit(5000);

        File.Delete(inputFilePath);
        return vgmProcess?.ExitCode == 0 && File.Exists(wavFilePath);
    }

    public static bool TryOpenAudioStream(string path, out Stream? stream)
    {
        if (File.Exists(path))
        {
            stream = new FileStream(path, FileMode.Open, FileAccess.Read);
            return true;
        }

        stream = null;
        return false;
    }

    private static string GetNewFileExtension(ESoundFormat soundFormat)
    {
        return soundFormat switch
        {
            ESoundFormat.WAV => ".wav",
            ESoundFormat.MP3 => ".mp3",
            ESoundFormat.OGG => ".ogg",
            ESoundFormat.FLAC => ".flac",
            _ => ".wav"
        };
    }
    
    extension(USoundCue root)
    {
        public List<Sound> HandleSoundTree(float offsetTime = 0.0f)
        {
            return root.FirstNode is null ? [] : HandleSoundTree(root.FirstNode.Load<USoundNode>(), offsetTime);
        }
    }

    extension(USoundNode? node)
    {
        public List<Sound> HandleSoundTree(float offsetTime = 0.0f)
        {
            var sounds = new List<Sound>();
            switch (node)
            {
                case USoundNodeWavePlayer player:
                {
                    sounds.Add(CreateSound(player, offsetTime));
                    break;
                }
                case USoundNodeDelay delay:
                {
                    foreach (var nodeObject in delay.ChildNodes) sounds.AddRange(HandleSoundTree(nodeObject.Load<USoundNode>(), offsetTime + delay.GetOrDefault("DelayMin", delay.GetOrDefault<float>("DelayMax"))));

                    break;
                }
                case USoundNodeRandom random:
                {
                    var index = Random.Shared.Next(0, random.ChildNodes.Length);
                    sounds.AddRange(HandleSoundTree(random.ChildNodes[index].Load<USoundNode>(), offsetTime));
                    break;
                }

                case UFortSoundNodeLicensedContentSwitcher switcher:
                {
                    sounds.AddRange(HandleSoundTree(switcher.ChildNodes.Last().Load<USoundNode>(), offsetTime));
                    break;
                }
                case USoundNodeDialoguePlayer dialoguePlayer:
                {
                    var dialogueWaveParameter = dialoguePlayer.Get<FStructFallback>("DialogueWaveParameter");
                    var dialogueWave = dialogueWaveParameter.Get<UDialogueWave>("DialogueWave");
                    var contextMappings = dialogueWave.Get<FStructFallback[]>("ContextMappings");
                    var soundWave = contextMappings.First().Get<FPackageIndex>("SoundWave");
                    sounds.Add(CreateSound(soundWave));
                    break;
                }
                case not null:
                {
                    foreach (var nodeObject in node.ChildNodes) sounds.AddRange(HandleSoundTree(nodeObject.Load<USoundNode>(), offsetTime));

                    break;
                }
            }

            return sounds;
        }
    }


    public static Sound CreateSound(USoundNodeWavePlayer player, float timeOffset = 0)
    {
        return new Sound(player.SoundWave, timeOffset, player.GetOrDefault("bLooping", false));
    }

    public static Sound CreateSound(FPackageIndex soundWave, float timeOffset = 0)
    {
        return new Sound(soundWave, timeOffset, false);
    }
}

public class Sound
{
    public FPackageIndex SoundWave;
    public float Time;
    public bool Loop;

    public Sound(FPackageIndex soundWave, float time, bool loop)
    {
        SoundWave = soundWave;
        Time = time;
        Loop = loop;
    }
}
