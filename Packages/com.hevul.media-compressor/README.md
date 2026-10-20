# Unity Media Compressor 🎬🖼️

[![Platform](https://img.shields.io/badge/Platform-Android-3DDC84.svg?style=flat&logo=android)](https://www.android.com/)
[![Unity](https://img.shields.io/badge/Unity-2022.3%2B-blue.svg?style=flat&logo=unity)](https://unity.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)](#contributing)

A high-performance, asynchronous media compression package for **Unity on Android**. It leverages Android's hardware-accelerated `MediaCodec` and `MediaMuxer` pipeline for video re-encoding, alongside multithreaded JPEG downscaling for images — achieving **60% to 85% file size reductions** while preserving visually near-lossless fidelity.

---

## ✨ Features

- 🚀 **Hardware Accelerated**: Uses native Android `MediaCodec` (GPU/SoC encoder), meaning **zero CPU overheating**, high-speed processing, and **no heavy FFmpeg binaries** (+0 MB build overhead).
- 🎬 **Smart Video Compression**:
  - H.264 / AVC output format with Variable Bitrate (VBR).
  - Handles portrait & landscape camera orientation tags (`setOrientationHint`) automatically.
  - Fixes presentation timestamps (PTS) down to nanosecond precision to avoid desync and crashes.
  - Automatically targets 1080p / 720p with adaptive bitrates.
- 🖼️ **High-Fidelity Image Compression**:
  - Intelligent resolution scaling (up to Full HD 1080p).
  - Quality-tuned JPEG encoding (default 82%) written asynchronously to keep your game UI completely smooth.
- 💾 **Gallery Export Option**:
  - Configurable `saveToGallery` flag (defaults to `false`).
  - Safely copies compressed media directly to the device's public photo gallery when requested.
- 🧹 **Built-in Cache Management**:
  - One-line cache clearing API (`ClearCache()`) to purge temporary local files once uploaded to your server.
  - Gallery-saved items remain safely preserved even when cache is wiped.
- ⚡ **Async / Await First**: Modern C# `Task`-based asynchronous methods with progress callbacks.

---

## 📦 Installation

### Option A: Via Unity Package Manager (Git URL) — Recommended
1. In Unity, open **Window** > **Package Manager**.
2. Click the **`+`** icon in the top-left corner and select **"Add package from git URL..."**.
3. Enter your repository URL:
   ```text
   https://github.com/your-username/unity-media-compressor.git
   ```

### Option B: Via `Packages/manifest.json`
Add the dependency directly to your `Packages/manifest.json`:
```json
{
  "dependencies": {
    "com.hevul.media-compressor": "https://github.com/your-username/unity-media-compressor.git",
    "com.yasirkula.nativegallery": "https://github.com/yasirkula/UnityNativeGallery.git"
  }
}
```

> **Note**: This package depends on `UnityNativeGallery` for memory-safe image decoding.

---

## 🚀 Quick Start Guide

### 1. Compress a Video Asynchronously

```csharp
using System.Threading.Tasks;
using UnityEngine;
using Hevul.MediaCompressor;

public class VideoCompressExample : MonoBehaviour
{
    public async Task CompressMyVideo(string inputVideoPath)
    {
        // Simple 1-line call (saveToGallery defaults to false)
        CompressionResult result = await MediaCompressor.CompressVideoAsync(
            inputPath: inputVideoPath,
            saveToGallery: false, // Set to true if you want a copy in the phone's gallery
            onProgress: (progress) =>
            {
                Debug.Log($"Compressing: {progress * 100f:F0}%");
            }
        );

        if (result.Success)
        {
            Debug.Log($"✅ Compressed video saved at: {result.OutputPath}");
            Debug.Log($"📊 Size: {result.OriginalSizeBytes} -> {result.CompressedSizeBytes} bytes");
            Debug.Log($"📉 Reduced by: {result.SavedPercentage:F1}%");

            // Example: Upload result.OutputPath to your backend server here...
        }
        else
        {
            Debug.LogError($"❌ Video compression failed: {result.ErrorMessage}");
        }
    }
}
```

---

### 2. Compress an Image Asynchronously

```csharp
using System.Threading.Tasks;
using UnityEngine;
using Hevul.MediaCompressor;

public class ImageCompressExample : MonoBehaviour
{
    public async Task CompressMyImage(string inputImagePath)
    {
        CompressionResult result = await MediaCompressor.CompressImageAsync(
            inputPath: inputImagePath,
            saveToGallery: false, // Set to true to export to device Gallery
            quality: 82,          // JPEG Quality (10-100)
            maxDimension: 1920    // Max resolution bound (Full HD)
        );

        if (result.Success)
        {
            Debug.Log($"✅ Compressed image saved at: {result.OutputPath}");
            Debug.Log($"📉 Reduced by: {result.SavedPercentage:F1}%");

            // Example: Upload result.OutputPath to your backend server here...
        }
        else
        {
            Debug.LogError($"❌ Image compression failed: {result.ErrorMessage}");
        }
    }
}
```

---

### 3. Clear Cache (After Upload to Server)

When your file has finished uploading to your cloud storage or server, call `ClearCache()` to free up device storage:

```csharp
// Deletes all compressed temporary files in the app's persistent cache directory
MediaCompressor.ClearCache();

// Or delete a specific file individually:
MediaCompressor.DeleteCompressedFile(result.OutputPath);
```

> 💡 **Gallery Protection**: Any files exported to the device's public Gallery album (`saveToGallery: true`) will **NOT** be deleted by `ClearCache()`. Only local temporary cached files are cleaned.

---

## 📋 API Reference

### `MediaCompressor` Static Class

| Method | Return Type | Description |
| :--- | :--- | :--- |
| `CompressVideoAsync(path, saveToGallery, onProgress, maxDimension, bitrate)` | `Task<CompressionResult>` | Compresses MP4/video using hardware MediaCodec. |
| `CompressImageAsync(path, saveToGallery, quality, maxDimension)` | `Task<CompressionResult>` | Scales and compresses images to high-quality JPEG asynchronously. |
| `ClearCache()` | `void` | Purges all temporary compressed media files from app storage. |
| `DeleteCompressedFile(filePath)` | `void` | Deletes a specific compressed file from cache. |
| `CacheDirectory` | `string` | Gets the absolute local directory where compressed media is stored. |

### `CompressionResult` Struct

| Property | Type | Description |
| :--- | :--- | :--- |
| `Success` | `bool` | `true` if compression finished without errors. |
| `OutputPath` | `string` | Absolute file path to the compressed media on device storage. |
| `OriginalSizeBytes` | `long` | Size of the input file in bytes. |
| `CompressedSizeBytes` | `long` | Size of the output file in bytes. |
| `SavedPercentage` | `float` | Percentage of file size reduced (e.g. `74.5%`). |
| `SavedToGallery` | `bool` | Indicates whether a copy was written to the public Gallery. |
| `ErrorMessage` | `string` | Error description if `Success` is `false`. |

---

## 📱 Android Requirements & Player Settings

Under **Project Settings** > **Player** > **Android**:
- **Minimum API Level**: Android 7.0 (API Level 24) or higher *(Recommended: API 26+)*.
- **Scripting Backend**: IL2CPP or Mono.
- **Target Architectures**: ARM64 and ARMv7 supported.

---

## 🛠️ Export as `.unitypackage`

To export this package as a standalone `.unitypackage` asset for distribution:
1. In Unity's top menu bar, click:
   $$\text{\textbf{Tools}} \longrightarrow \text{\textbf{Media Compressor}} \longrightarrow \text{\textbf{Export .unitypackage}}$$
2. Choose your destination file name and save.

---

## 📄 License

This package is licensed under the [MIT License](LICENSE).
