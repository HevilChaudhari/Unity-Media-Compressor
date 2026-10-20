using System;
using System.Collections.Concurrent;
using System.IO;
using UnityEngine;

namespace Hevul.MediaCompressor
{
    internal class VideoCompressorBridge : MonoBehaviour
    {
        private static VideoCompressorBridge instance;
        private static readonly ConcurrentQueue<Action> mainThreadQueue = new ConcurrentQueue<Action>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (instance == null)
            {
                GameObject go = new GameObject("[Hevul_VideoCompressor_Dispatcher]");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<VideoCompressorBridge>();
            }
        }

        private void Update()
        {
            while (mainThreadQueue.TryDequeue(out Action action))
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogError("[VideoCompressorBridge] Dispatch exception: " + ex);
                }
            }
        }

        internal static void RunOnMainThread(Action action)
        {
            if (action == null) return;
            mainThreadQueue.Enqueue(action);
        }

        private class AndroidCompressionCallback : AndroidJavaProxy
        {
            private readonly Action<float> progressCallback;
            private readonly Action<string, long, long> successCallback;
            private readonly Action<string> failureCallback;

            public AndroidCompressionCallback(Action<float> onProgress, Action<string, long, long> onSuccess, Action<string> onFailure)
                : base("com.hevul.videocompressor.NativeVideoCompressor$CompressionCallback")
            {
                this.progressCallback = onProgress;
                this.successCallback = onSuccess;
                this.failureCallback = onFailure;
            }

            public void onProgress(float percent)
            {
                RunOnMainThread(() => progressCallback?.Invoke(percent));
            }

            public void onSuccess(string outputPath, long originalSize, long compressedSize)
            {
                RunOnMainThread(() => successCallback?.Invoke(outputPath, originalSize, compressedSize));
            }

            public void onFailure(string errorMessage)
            {
                RunOnMainThread(() => failureCallback?.Invoke(errorMessage));
            }
        }

        internal static void CompressVideoInternal(string inputPath,
                                                   string outputPath,
                                                   int targetWidth,
                                                   int targetHeight,
                                                   int targetBitrate,
                                                   Action<float> onProgress,
                                                   Action<string, long, long> onSuccess,
                                                   Action<string> onFailure)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaClass compressorClass = new AndroidJavaClass("com.hevul.videocompressor.NativeVideoCompressor"))
                {
                    AndroidCompressionCallback proxy = new AndroidCompressionCallback(onProgress, onSuccess, onFailure);
                    compressorClass.CallStatic("compressVideo",
                        currentActivity,
                        inputPath,
                        outputPath,
                        targetWidth,
                        targetHeight,
                        targetBitrate,
                        proxy);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[VideoCompressorBridge] Android call error: " + ex.Message);
                RunOnMainThread(() => onFailure?.Invoke(ex.Message));
            }
#else
            if (File.Exists(inputPath))
            {
                long origSize = new FileInfo(inputPath).Length;
                File.Copy(inputPath, outputPath, true);
                long compSize = new FileInfo(outputPath).Length;
                RunOnMainThread(() =>
                {
                    onProgress?.Invoke(1.0f);
                    onSuccess?.Invoke(outputPath, origSize, compSize);
                });
            }
            else
            {
                RunOnMainThread(() => onFailure?.Invoke("Input file does not exist: " + inputPath));
            }
#endif
        }
    }
}
