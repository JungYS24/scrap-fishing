using UnityEditor;
using UnityEngine;

public class AudioImportPostprocessor : AssetPostprocessor
{
    void OnPreprocessAudio()
    {
        var importer = (AudioImporter)assetImporter;
        var path = importer.assetPath.Replace('\\', '/');
        if (!path.StartsWith("Assets/Resources/Audio/"))
        {
            return;
        }

        if (path.Contains("/SFX/"))
        {
            ApplySfx(importer);
            return;
        }

        if (path.EndsWith(".mp3", System.StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".ogg", System.StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".wav", System.StringComparison.OrdinalIgnoreCase))
        {
            ApplyBgm(importer);
        }
    }

    static void ApplySfx(AudioImporter importer)
    {
        importer.forceToMono = true;
        importer.loadInBackground = false;
        var settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.DecompressOnLoad;
        settings.preloadAudioData = true;
        settings.compressionFormat = AudioCompressionFormat.ADPCM;
        settings.quality = 1f;
        importer.defaultSampleSettings = settings;

        var webgl = settings;
        webgl.compressionFormat = AudioCompressionFormat.Vorbis;
        webgl.quality = 0.85f;
        webgl.loadType = AudioClipLoadType.DecompressOnLoad;
        webgl.preloadAudioData = true;
        importer.SetOverrideSampleSettings("WebGL", webgl);
        if (importer.assetPath.EndsWith("/sfx_chip_tick.mp3", System.StringComparison.OrdinalIgnoreCase))
        {
            var so = new SerializedObject(importer);
            var normalize = so.FindProperty("m_Normalize") ?? so.FindProperty("normalize");
            if (normalize != null)
            {
                normalize.boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    static void ApplyBgm(AudioImporter importer)
    {
        importer.forceToMono = false;
        importer.loadInBackground = false;
        var settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.CompressedInMemory;
        settings.preloadAudioData = true;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = 0.6f;
        importer.defaultSampleSettings = settings;
        importer.SetOverrideSampleSettings("WebGL", settings);
    }
}
