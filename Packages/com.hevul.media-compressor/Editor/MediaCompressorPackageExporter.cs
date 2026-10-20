#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hevul.MediaCompressor.Editor
{
    public static class MediaCompressorPackageExporter
    {
        [MenuItem("Tools/Media Compressor/Export .unitypackage")]
        public static void ExportUnityPackage()
        {
            string packageFolder = "Packages/com.hevul.media-compressor";
            if (!Directory.Exists(packageFolder))
            {
                EditorUtility.DisplayDialog("Error", $"Package folder not found at {packageFolder}", "OK");
                return;
            }

            string defaultPath = Path.Combine(Directory.GetCurrentDirectory(), "MediaCompressor_v1.0.0.unitypackage");
            string exportPath = EditorUtility.SaveFilePanel("Export Media Compressor Package", "", "MediaCompressor_v1.0.0", "unitypackage");

            if (string.IsNullOrEmpty(exportPath)) return;

            AssetDatabase.ExportPackage(
                packageFolder,
                exportPath,
                ExportPackageOptions.Recurse
            );

            EditorUtility.RevealInFinder(exportPath);
            Debug.Log($"[MediaCompressor] Successfully exported to: {exportPath}");
        }
    }
}
#endif
