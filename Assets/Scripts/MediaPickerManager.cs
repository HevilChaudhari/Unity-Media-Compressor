using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using Hevul.MediaCompressor; // Standalone Package API

public class MediaPickerManager : MonoBehaviour
{
    public enum MediaType
    {
        None,
        Image,
        Video
    }

    [Header("UI Preview Elements")]
    [SerializeField] private RawImage previewImage;
    [SerializeField] private VideoPlayer videoPlayer;

    [Header("UI Status & Info Elements")]
    [SerializeField] private Text statusText;
    [SerializeField] private Text originalSizeText;
    [SerializeField] private Text compressedSizeText;
    [SerializeField] private Button togglePreviewButton;

    [Header("Compression Options")]
    [Tooltip("If true, a copy of the compressed output will be exported to the phone's public Gallery album. Defaults to false.")]
    [SerializeField] private bool saveCompressedToGallery = false;

    // Track active media paths for comparison
    private string originalFilePath = "";
    private string compressedFilePath = "";
    private MediaType currentMediaType = MediaType.None;
    private long originalSizeBytes = 0;
    private long compressedSizeBytes = 0;
    private bool isShowingCompressedPreview = true;

    private RenderTexture videoRenderTexture;
    private Texture2D currentLoadedTexture;

    private void Start()
    {
        UpdateStatus("Ready. Select or capture media to begin.");
        ResetDisplays();

        if (videoPlayer != null)
        {
            videoRenderTexture = new RenderTexture(1080, 1920, 0);
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture = videoRenderTexture;
            videoPlayer.isLooping = true;
        }

        if (previewImage != null)
        {
            previewImage.gameObject.SetActive(false);
        }

        if (togglePreviewButton != null)
        {
            togglePreviewButton.onClick.AddListener(OnTogglePreviewClicked);
            togglePreviewButton.gameObject.SetActive(false);
        }
    }

    private void ResetDisplays()
    {
        if (originalSizeText != null) originalSizeText.text = "Original Size: --";
        if (compressedSizeText != null) compressedSizeText.text = "Compressed Size: --";
        if (togglePreviewButton != null) togglePreviewButton.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (videoRenderTexture != null)
        {
            videoRenderTexture.Release();
            Destroy(videoRenderTexture);
        }

        if (currentLoadedTexture != null)
        {
            Destroy(currentLoadedTexture);
        }
    }

    #region Media Acquisition Handlers

    public void OnPickImageClicked()
    {
        if (NativeGallery.IsMediaPickerBusy())
        {
            UpdateStatus("Media picker is already active.");
            return;
        }

        UpdateStatus("Opening Gallery for image...");

        NativeGallery.GetImageFromGallery((path) =>
        {
            if (string.IsNullOrEmpty(path))
            {
                UpdateStatus("Image pick cancelled.");
                return;
            }

            StartCoroutine(ProcessAcquiredMediaCoroutine(path, MediaType.Image, "Gallery Image", isCameraCapture: false));
        }, "Select an Image", "image/*");
    }

    public void OnPickVideoClicked()
    {
        if (NativeGallery.IsMediaPickerBusy())
        {
            UpdateStatus("Media picker is already active.");
            return;
        }

        UpdateStatus("Opening Gallery for video...");

        NativeGallery.GetVideoFromGallery((path) =>
        {
            if (string.IsNullOrEmpty(path))
            {
                UpdateStatus("Video pick cancelled.");
                return;
            }

            StartCoroutine(ProcessAcquiredMediaCoroutine(path, MediaType.Video, "Gallery Video", isCameraCapture: false));
        }, "Select a Video");
    }

    public void OnCaptureImageClicked()
    {
        if (NativeCamera.IsCameraBusy())
        {
            UpdateStatus("Camera is already busy.");
            return;
        }

        UpdateStatus("Opening Camera for photo...");

        NativeCamera.TakePicture((path) =>
        {
            if (string.IsNullOrEmpty(path))
            {
                UpdateStatus("Photo capture cancelled.");
                return;
            }

            StartCoroutine(ProcessAcquiredMediaCoroutine(path, MediaType.Image, "Captured Photo", isCameraCapture: true));
        }, maxSize: 2560);
    }

    public void OnRecordVideoClicked()
    {
        if (NativeCamera.IsCameraBusy())
        {
            UpdateStatus("Camera is already busy.");
            return;
        }

        UpdateStatus("Opening Camera for video recording...");

        NativeCamera.RecordVideo((path) =>
        {
            if (string.IsNullOrEmpty(path))
            {
                UpdateStatus("Video recording cancelled.");
                return;
            }

            StartCoroutine(ProcessAcquiredMediaCoroutine(path, MediaType.Video, "Recorded Video", isCameraCapture: true));
        }, NativeCamera.Quality.High, maxDuration: 60);
    }

    #endregion

    private IEnumerator ProcessAcquiredMediaCoroutine(string path, MediaType type, string sourceLabel, bool isCameraCapture)
    {
        currentMediaType = type;
        originalFilePath = path;
        compressedFilePath = "";

        yield return new WaitForSeconds(0.35f);

        int attempts = 0;
        while (attempts < 10)
        {
            if (File.Exists(path))
            {
                originalSizeBytes = new FileInfo(path).Length;
                if (originalSizeBytes > 0) break;
            }
            yield return new WaitForSeconds(0.15f);
            attempts++;
        }

        compressedSizeBytes = 0;
        string origSizeStr = FormatBytes(originalSizeBytes);
        UpdateStatus($"Loaded {sourceLabel}: {Path.GetFileName(path)} ({origSizeStr})");

        if (originalSizeText != null) originalSizeText.text = $"Original: {origSizeStr} ({type})";
        if (compressedSizeText != null) compressedSizeText.text = "Compressed: Compressing now...";
        if (togglePreviewButton != null) togglePreviewButton.gameObject.SetActive(false);

        if (type == MediaType.Image)
        {
            DisplayImage(path);
            _ = CompressImageAsync(path);
        }
        else if (type == MediaType.Video)
        {
            _ = CompressVideoAsync(path);
        }

        if (isCameraCapture)
        {
            SaveOriginalToGallery(path, type);
        }
    }

    private void SaveOriginalToGallery(string path, MediaType type)
    {
        try
        {
            if (type == MediaType.Image)
            {
                NativeGallery.SaveImageToGallery(path, "VideoCompression_Captures", Path.GetFileName(path), null);
            }
            else if (type == MediaType.Video)
            {
                NativeGallery.SaveVideoToGallery(path, "VideoCompression_Captures", Path.GetFileName(path), null);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[MediaPickerManager] Could not save capture to gallery: " + ex.Message);
        }
    }

    #region Async Compression using Hevul.MediaCompressor Package

    private async Task CompressImageAsync(string sourcePath)
    {
        UpdateStatus("Compressing image...");

        // Calling standalone package static method
        CompressionResult result = await MediaCompressor.CompressImageAsync(
            sourcePath,
            saveToGallery: saveCompressedToGallery
        );

        if (result.Success)
        {
            compressedFilePath = result.OutputPath;
            originalSizeBytes = result.OriginalSizeBytes;
            compressedSizeBytes = result.CompressedSizeBytes;

            string origStr = FormatBytes(originalSizeBytes);
            string compStr = FormatBytes(compressedSizeBytes);

            UpdateStatus($"✅ Image Compressed! Saved {result.SavedPercentage:F1}%");

            if (originalSizeText != null) originalSizeText.text = $"Original: {origStr}";
            if (compressedSizeText != null) compressedSizeText.text = $"Compressed: {compStr} (-{result.SavedPercentage:F1}%)";

            isShowingCompressedPreview = true;
            DisplayImage(compressedFilePath);
            UpdateToggleButton();
        }
        else
        {
            Debug.LogError("[MediaPickerManager] " + result.ErrorMessage);
            UpdateStatus("Image compression failed: " + result.ErrorMessage);
        }
    }

    private async Task CompressVideoAsync(string sourcePath)
    {
        UpdateStatus("Compressing video via hardware...");

        // Calling standalone package static method
        CompressionResult result = await MediaCompressor.CompressVideoAsync(
            sourcePath,
            saveToGallery: saveCompressedToGallery,
            onProgress: (progress) =>
            {
                UpdateStatus($"Compressing video: {(progress * 100f):F0}%");
            }
        );

        if (result.Success)
        {
            compressedFilePath = result.OutputPath;
            originalSizeBytes = result.OriginalSizeBytes;
            compressedSizeBytes = result.CompressedSizeBytes;

            string origStr = FormatBytes(originalSizeBytes);
            string compStr = FormatBytes(compressedSizeBytes);

            UpdateStatus($"✅ Video Compressed! Saved {result.SavedPercentage:F1}%");

            if (originalSizeText != null) originalSizeText.text = $"Original: {origStr}";
            if (compressedSizeText != null) compressedSizeText.text = $"Compressed: {compStr} (-{result.SavedPercentage:F1}%)";

            isShowingCompressedPreview = true;
            PlayVideo(compressedFilePath);
            UpdateToggleButton();
        }
        else
        {
            Debug.LogError("[MediaPickerManager] " + result.ErrorMessage);
            UpdateStatus("Video compression failed: " + result.ErrorMessage);
        }
    }

    /// <summary>
    /// Example method demonstrating cache cleanup after upload
    /// </summary>
    public void OnUploadCompletedDemo()
    {
        // Delete all cached compressed videos/images
        MediaCompressor.ClearCache();
        UpdateStatus("Cache cleared after upload!");
    }

    #endregion

    #region Comparison & Preview Controls

    public void OnTogglePreviewClicked()
    {
        if (string.IsNullOrEmpty(originalFilePath) || string.IsNullOrEmpty(compressedFilePath)) return;

        isShowingCompressedPreview = !isShowingCompressedPreview;
        string activePath = isShowingCompressedPreview ? compressedFilePath : originalFilePath;
        string tag = isShowingCompressedPreview ? "[COMPRESSED]" : "[ORIGINAL]";

        UpdateStatus($"Now Viewing: {tag} ({FormatBytes(new FileInfo(activePath).Length)})");

        if (currentMediaType == MediaType.Image)
        {
            DisplayImage(activePath);
        }
        else if (currentMediaType == MediaType.Video)
        {
            PlayVideo(activePath);
        }

        UpdateToggleButton();
    }

    private void UpdateToggleButton()
    {
        if (togglePreviewButton == null) return;

        togglePreviewButton.gameObject.SetActive(!string.IsNullOrEmpty(compressedFilePath));
        Text btnText = togglePreviewButton.GetComponentInChildren<Text>();
        if (btnText != null)
        {
            btnText.text = isShowingCompressedPreview
                ? "👀 Switch to Original"
                : "⚡ Switch to Compressed";
        }
    }

    private void DisplayImage(string path)
    {
        if (videoPlayer != null && videoPlayer.isPlaying)
        {
            videoPlayer.Stop();
        }

        if (currentLoadedTexture != null)
        {
            Destroy(currentLoadedTexture);
        }

        currentLoadedTexture = NativeGallery.LoadImageAtPath(path, maxSize: 1920, markTextureNonReadable: false);
        if (currentLoadedTexture != null && previewImage != null)
        {
            previewImage.texture = currentLoadedTexture;
            previewImage.gameObject.SetActive(true);
        }
    }

    private void PlayVideo(string path)
    {
        if (videoPlayer == null) return;

        if (videoPlayer.isPlaying)
        {
            videoPlayer.Stop();
        }

        if (previewImage != null)
        {
            previewImage.texture = videoRenderTexture;
            previewImage.gameObject.SetActive(true);
        }

        videoPlayer.url = path;
        videoPlayer.Play();
    }

    #endregion

    private void UpdateStatus(string message)
    {
        Debug.Log("[MediaPickerManager] " + message);
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    private string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";
        if (bytes >= 1024 * 1024)
            return $"{bytes / (1024f * 1024f):F2} MB";
        if (bytes >= 1024)
            return $"{bytes / 1024f:F2} KB";
        return $"{bytes} B";
    }
}
