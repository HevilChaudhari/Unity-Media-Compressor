# Media Compressor (Standalone Unity Package)

A high-performance, asynchronous media compression package for Unity Android.

- **Video Compression**: Uses Android's native hardware `MediaCodec` + `MediaMuxer` (VBR mode, 1080p/720p adaptive bitrates).
- **Image Compression**: High-quality (82%) asynchronous JPEG encoding with intelligent Full HD downscaling.
- **Gallery Export**: Optional `saveToGallery` flag (defaults to `false`).
- **Cache Management**: One-line `ClearCache()` to clean temporary compressed files after uploading to a server.

---

## Installation in Other Projects

You can copy the `Packages/com.hevul.media-compressor` folder into any other Unity project's `Packages/` folder, or add it to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.hevul.media-compressor": "file:../path/to/com.hevul.media-compressor",
    "com.yasirkula.nativegallery": "https://github.com/yasirkula/UnityNativeGallery.git"
  }
}
```

---

## API Reference

### 1. Compress Video Asynchronously

```csharp
using Hevul.MediaCompressor;

// Simple usage (defaults: saveToGallery = false)
CompressionResult result = await MediaCompressor.CompressVideoAsync(
    inputPath: "/storage/emulated/0/.../input.mp4",
    saveToGallery: false, // Set to true if you want a permanent copy in the device Gallery
    onProgress: (progress) => {
        Debug.Log($"Progress: {progress * 100f:F0}%");
    }
);

if (result.Success)
{
    Debug.Log($"Compressed path: {result.OutputPath}");
    Debug.Log($"Original Size: {result.OriginalSizeBytes} bytes");
    Debug.Log($"Compressed Size: {result.CompressedSizeBytes} bytes");
    Debug.Log($"Saved: {result.SavedPercentage:F1}%");

    // Upload to server using result.OutputPath...
}
else
{
    Debug.LogError($"Compression failed: {result.ErrorMessage}");
}
```

---

### 2. Compress Image Asynchronously

```csharp
using Hevul.MediaCompressor;

CompressionResult result = await MediaCompressor.CompressImageAsync(
    inputPath: "/storage/emulated/0/.../photo.jpg",
    saveToGallery: false // Defaults to false
);

if (result.Success)
{
    Debug.Log($"Compressed image path: {result.OutputPath}");
    // Upload result.OutputPath to server...
}
```

---

### 3. Clear Cache (After Uploading to Server)

When your file has finished uploading to the server, call `ClearCache()`:

```csharp
// Deletes all compressed temporary files in local persistent cache
MediaCompressor.ClearCache();
```

> **Note**: If `saveToGallery: true` was passed, the copy saved in the device's public Gallery album remains untouched and safe. Only local temporary cache files are cleared.

To delete an individual file:
```csharp
MediaCompressor.DeleteCompressedFile(result.OutputPath);
```
