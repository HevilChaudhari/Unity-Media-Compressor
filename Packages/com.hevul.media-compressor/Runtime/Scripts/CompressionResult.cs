namespace Hevul.MediaCompressor
{
    public struct CompressionResult
    {
        public bool Success;
        public string OutputPath;
        public long OriginalSizeBytes;
        public long CompressedSizeBytes;
        public float SavedPercentage;
        public bool SavedToGallery;
        public string ErrorMessage;

        public override string ToString()
        {
            if (!Success) return $"Compression Failed: {ErrorMessage}";
            return $"Success: {OutputPath} | Orig: {FormatBytes(OriginalSizeBytes)} | Comp: {FormatBytes(CompressedSizeBytes)} (-{SavedPercentage:F1}%) | Gallery: {SavedToGallery}";
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "0 B";
            if (bytes >= 1024 * 1024) return $"{bytes / (1024f * 1024f):F2} MB";
            if (bytes >= 1024) return $"{bytes / 1024f:F2} KB";
            return $"{bytes} B";
        }
    }
}
