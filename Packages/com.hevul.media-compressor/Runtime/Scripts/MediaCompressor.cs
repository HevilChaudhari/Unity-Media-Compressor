using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace Hevul.MediaCompressor
{
    /// <summary>
    /// Standalone Media Compressor API providing asynchronous image and hardware video compression
    /// with optional gallery export and cache management.
    /// </summary>
    public static class MediaCompressor
    {
        private const string CACHE_FOLDER_NAME = "Hevul_CompressedMedia";
        private const string GALLERY_ALBUM_NAME = "MediaCompressor";

        /// <summary>
        /// Absolute directory where compressed files are cached locally
        /// </summary>
        public static string CacheDirectory
        {
            get
            {
                string dir = Path.Combine(Application.persistentDataPath, CACHE_FOLDER_NAME);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                return dir;
            }
        }

        #region Public Static Async Video Compression

        /// <summary>
        /// Compress video asynchronously using Android Hardware MediaCodec (or simulated in Editor).
        /// </summary>
        /// <param name="inputPath">Full path to the source video file</param>
        /// <param name="saveToGallery">If true, exports a copy of the compressed video into device's public Gallery album</param>
        /// <param name="onProgress">Optional progress callback (0.0f to 1.0f)</param>
        /// <param name="targetMaxDimension">Max resolution dimension (default 1920 Full HD)</param>
        /// <param name="targetBitrate">Bitrate in bps (0 for smart adaptive VBR)</param>
        /// <returns>Task containing CompressionResult with OutputPath, original/compressed sizes, and status</returns>
        public static Task<CompressionResult> CompressVideoAsync(
            string inputPath,
            bool saveToGallery = false,
            Action<float> onProgress = null,
            int targetMaxDimension = 1920,
            int targetBitrate = 0)
        {
            var tcs = new TaskCompletionSource<CompressionResult>();

            if (string.IsNullOrEmpty(inputPath) || !File.Exists(inputPath))
            {
                tcs.SetResult(new CompressionResult
                {
                    Success = false,
                    ErrorMessage = $"Input file does not exist: {inputPath}"
                });
                return tcs.Task;
            }

            long originalSize = new FileInfo(inputPath).Length;
            string outFileName = $"VID_comp_{DateTime.Now.Ticks}.mp4";
            string outputPath = Path.Combine(CacheDirectory, outFileName);

            VideoCompressorBridge.CompressVideoInternal(
                inputPath,
                outputPath,
                targetMaxDimension,
                targetMaxDimension,
                targetBitrate,
                onProgress: (progress) =>
                {
                    onProgress?.Invoke(progress);
                },
                onSuccess: (finalPath, origSize, compSize) =>
                {
                    bool exported = false;
                    if (saveToGallery)
                    {
                        try
                        {
                            NativeGallery.SaveVideoToGallery(finalPath, GALLERY_ALBUM_NAME, outFileName, null);
                            exported = true;
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning("[MediaCompressor] Gallery save warning: " + ex.Message);
                        }
                    }

                    float savedPercent = (origSize > 0) ? (1.0f - ((float)compSize / origSize)) * 100f : 0f;

                    tcs.SetResult(new CompressionResult
                    {
                        Success = true,
                        OutputPath = finalPath,
                        OriginalSizeBytes = origSize,
                        CompressedSizeBytes = compSize,
                        SavedPercentage = savedPercent,
                        SavedToGallery = exported
                    });
                },
                onFailure: (error) =>
                {
                    tcs.SetResult(new CompressionResult
                    {
                        Success = false,
                        ErrorMessage = error
                    });
                }
            );

            return tcs.Task;
        }

        #endregion

        #region Public Static Async Image Compression

        /// <summary>
        /// Compress image asynchronously downscaling resolution and encoding to high quality JPEG.
        /// </summary>
        /// <param name="inputPath">Full path to the source image file</param>
        /// <param name="saveToGallery">If true, exports a copy of the compressed image into device's public Gallery album</param>
        /// <param name="quality">JPEG compression quality (1 to 100, default 82)</param>
        /// <param name="maxDimension">Maximum width or height in pixels (default 1920)</param>
        /// <returns>Task containing CompressionResult with OutputPath, sizes, and status</returns>
        public static async Task<CompressionResult> CompressImageAsync(
            string inputPath,
            bool saveToGallery = false,
            int quality = 82,
            int maxDimension = 1920)
        {
            if (string.IsNullOrEmpty(inputPath) || !File.Exists(inputPath))
            {
                return new CompressionResult
                {
                    Success = false,
                    ErrorMessage = $"Input file does not exist: {inputPath}"
                };
            }

            long originalSize = new FileInfo(inputPath).Length;
            Texture2D loaded = null;

            try
            {
                // NativeGallery downscaling must run on Unity main thread
                loaded = NativeGallery.LoadImageAtPath(inputPath, maxSize: maxDimension, markTextureNonReadable: false);
                if (loaded == null)
                {
                    return new CompressionResult
                    {
                        Success = false,
                        ErrorMessage = "Failed to decode image from path: " + inputPath
                    };
                }

                // Compress texture bytes
                byte[] compressedBytes = loaded.EncodeToJPG(Mathf.Clamp(quality, 10, 100));
                UnityEngine.Object.Destroy(loaded);
                loaded = null;

                string outFileName = $"IMG_comp_{DateTime.Now.Ticks}.jpg";
                string outputPath = Path.Combine(CacheDirectory, outFileName);

                // Write file asynchronously on threadpool to keep UI buttery smooth
                await Task.Run(() => File.WriteAllBytes(outputPath, compressedBytes));

                long compSize = compressedBytes.Length;
                bool exported = false;

                if (saveToGallery)
                {
                    try
                    {
                        NativeGallery.SaveImageToGallery(outputPath, GALLERY_ALBUM_NAME, outFileName, null);
                        exported = true;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[MediaCompressor] Gallery save warning: " + ex.Message);
                    }
                }

                float savedPercent = (originalSize > 0) ? (1.0f - ((float)compSize / originalSize)) * 100f : 0f;

                return new CompressionResult
                {
                    Success = true,
                    OutputPath = outputPath,
                    OriginalSizeBytes = originalSize,
                    CompressedSizeBytes = compSize,
                    SavedPercentage = savedPercent,
                    SavedToGallery = exported
                };
            }
            catch (Exception ex)
            {
                if (loaded != null) UnityEngine.Object.Destroy(loaded);
                return new CompressionResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        #endregion

        #region Public Static Cache Cleaning Methods

        /// <summary>
        /// Delete all compressed temporary files in the local cache directory.
        /// Call this after your files have finished uploading to the server.
        /// Note: Files saved to the device's public Gallery (when saveToGallery: true) remain safely untouched in the Gallery!
        /// </summary>
        public static void ClearCache()
        {
            try
            {
                string dir = CacheDirectory;
                if (Directory.Exists(dir))
                {
                    string[] files = Directory.GetFiles(dir);
                    foreach (string file in files)
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[MediaCompressor] Could not delete {file}: {ex.Message}");
                        }
                    }
                    Debug.Log($"[MediaCompressor] Cache cleared ({files.Length} files removed).");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[MediaCompressor] Error clearing cache: " + ex.Message);
            }
        }

        /// <summary>
        /// Delete a single compressed file from the local cache.
        /// </summary>
        /// <param name="filePath">Full path of the compressed file to delete</param>
        public static void DeleteCompressedFile(string filePath)
        {
            try
            {
                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Debug.Log($"[MediaCompressor] Deleted cached file: {filePath}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MediaCompressor] Could not delete {filePath}: {ex.Message}");
            }
        }

        #endregion
    }
}
