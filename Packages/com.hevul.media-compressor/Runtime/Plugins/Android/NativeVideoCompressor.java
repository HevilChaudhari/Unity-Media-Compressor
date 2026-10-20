package com.hevul.videocompressor;

import android.content.Context;
import android.media.MediaCodec;
import android.media.MediaCodecInfo;
import android.media.MediaExtractor;
import android.media.MediaFormat;
import android.media.MediaMetadataRetriever;
import android.media.MediaMuxer;
import android.os.Handler;
import android.os.Looper;
import android.util.Log;
import android.view.Surface;

import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.IOException;
import java.nio.ByteBuffer;
import java.nio.channels.FileChannel;

public class NativeVideoCompressor {
    private static final String TAG = "NativeVideoCompressor";
    private static final int TIMEOUT_USEC = 10000;

    public interface CompressionCallback {
        void onProgress(float percent);
        void onSuccess(String outputPath, long originalSize, long compressedSize);
        void onFailure(String errorMessage);
    }

    public static void compressVideo(final Context context,
                                     final String inputPath,
                                     final String outputPath,
                                     final int targetWidth,
                                     final int targetHeight,
                                     final int targetBitrate,
                                     final CompressionCallback callback) {
        new Thread(new Runnable() {
            @Override
            public void run() {
                try {
                    doCompress(inputPath, outputPath, targetWidth, targetHeight, targetBitrate, callback);
                } catch (Throwable t) {
                    Log.e(TAG, "Hardware transcode error: " + t.getMessage(), t);
                    postFailure(callback, "Video compression error: " + t.getMessage());
                }
            }
        }).start();
    }

    private static void doCompress(String inputPath, String outputPath,
                                   int targetWidth, int targetHeight, int targetBitrate,
                                   final CompressionCallback callback) throws IOException {
        File inputFile = new File(inputPath);
        if (!inputFile.exists()) {
            postFailure(callback, "Input file does not exist: " + inputPath);
            return;
        }

        final long originalSize = inputFile.length();
        File outputFile = new File(outputPath);
        if (outputFile.exists()) {
            outputFile.delete();
        }

        // 1. Read Rotation & Dimensions from MediaMetadataRetriever
        int videoRotation = 0;
        MediaMetadataRetriever retriever = new MediaMetadataRetriever();
        try {
            retriever.setDataSource(inputPath);
            String rotationStr = retriever.extractMetadata(MediaMetadataRetriever.METADATA_KEY_VIDEO_ROTATION);
            if (rotationStr != null) {
                videoRotation = Integer.parseInt(rotationStr);
            }
        } catch (Exception ex) {
            Log.w(TAG, "Could not extract rotation: " + ex.getMessage());
        } finally {
            try { retriever.release(); } catch (Exception ignored) {}
        }

        MediaExtractor videoExtractor = new MediaExtractor();
        videoExtractor.setDataSource(inputPath);

        MediaExtractor audioExtractor = new MediaExtractor();
        audioExtractor.setDataSource(inputPath);

        int videoTrackIndex = -1;
        int audioTrackIndex = -1;
        MediaFormat inputVideoFormat = null;
        MediaFormat inputAudioFormat = null;

        int trackCount = videoExtractor.getTrackCount();
        for (int i = 0; i < trackCount; i++) {
            MediaFormat format = videoExtractor.getTrackFormat(i);
            String mime = format.getString(MediaFormat.KEY_MIME);
            if (mime != null && mime.startsWith("video/") && videoTrackIndex < 0) {
                videoTrackIndex = i;
                inputVideoFormat = format;
            } else if (mime != null && mime.startsWith("audio/") && audioTrackIndex < 0) {
                audioTrackIndex = i;
                inputAudioFormat = format;
            }
        }

        if (videoTrackIndex < 0) {
            videoExtractor.release();
            audioExtractor.release();
            postFailure(callback, "No video track found in input file.");
            return;
        }

        videoExtractor.selectTrack(videoTrackIndex);

        long durationUs = 0;
        if (inputVideoFormat.containsKey(MediaFormat.KEY_DURATION)) {
            durationUs = inputVideoFormat.getLong(MediaFormat.KEY_DURATION);
        }

        int srcWidth = inputVideoFormat.getInteger(MediaFormat.KEY_WIDTH);
        int srcHeight = inputVideoFormat.getInteger(MediaFormat.KEY_HEIGHT);

        // Keep Full HD clarity: scale down only if > 1920
        int maxDimension = Math.max(targetWidth, targetHeight);
        if (maxDimension <= 0) maxDimension = 1920;

        int outWidth = srcWidth;
        int outHeight = srcHeight;
        if (Math.max(srcWidth, srcHeight) > maxDimension) {
            if (srcWidth > srcHeight) {
                outWidth = maxDimension;
                outHeight = (int) ((float) srcHeight * ((float) maxDimension / (float) srcWidth));
            } else {
                outHeight = maxDimension;
                outWidth = (int) ((float) srcWidth * ((float) maxDimension / (float) srcHeight));
            }
        }

        // Even dimensions required for H.264
        outWidth = (outWidth / 2) * 2;
        outHeight = (outHeight / 2) * 2;

        int bitrate = targetBitrate;
        if (bitrate <= 0) {
            long totalPixels = (long) outWidth * outHeight;
            if (totalPixels >= 1920 * 1080) {
                bitrate = 2600000; // 2.6 Mbps for crisp 1080p
            } else if (totalPixels >= 1280 * 720) {
                bitrate = 1800000; // 1.8 Mbps for crisp 720p
            } else {
                bitrate = 1200000;
            }
        }

        int frameRate = 30;
        if (inputVideoFormat.containsKey(MediaFormat.KEY_FRAME_RATE)) {
            try {
                frameRate = inputVideoFormat.getInteger(MediaFormat.KEY_FRAME_RATE);
            } catch (Exception ignored) {}
        }

        MediaCodec encoder = null;
        MediaCodec decoder = null;
        MediaMuxer muxer = null;

        try {
            // 2. Configure H.264 Surface Encoder
            MediaFormat outputFormat = MediaFormat.createVideoFormat(MediaFormat.MIMETYPE_VIDEO_AVC, outWidth, outHeight);
            outputFormat.setInteger(MediaFormat.KEY_COLOR_FORMAT, MediaCodecInfo.CodecCapabilities.COLOR_FormatSurface);
            outputFormat.setInteger(MediaFormat.KEY_BIT_RATE, bitrate);
            outputFormat.setInteger(MediaFormat.KEY_FRAME_RATE, frameRate);
            outputFormat.setInteger(MediaFormat.KEY_I_FRAME_INTERVAL, 1);
            outputFormat.setInteger(MediaFormat.KEY_BITRATE_MODE, MediaCodecInfo.EncoderCapabilities.BITRATE_MODE_VBR);

            encoder = MediaCodec.createEncoderByType(MediaFormat.MIMETYPE_VIDEO_AVC);
            encoder.configure(outputFormat, null, null, MediaCodec.CONFIGURE_FLAG_ENCODE);
            Surface encoderInputSurface = encoder.createInputSurface();
            encoder.start();

            // 3. Configure Video Decoder directly targeting Encoder Surface
            String videoMime = inputVideoFormat.getString(MediaFormat.KEY_MIME);
            decoder = MediaCodec.createDecoderByType(videoMime);
            decoder.configure(inputVideoFormat, encoderInputSurface, null, 0);
            decoder.start();

            // 4. Configure Muxer
            muxer = new MediaMuxer(outputPath, MediaMuxer.OutputFormat.MUXER_OUTPUT_MPEG_4);
            if (videoRotation != 0) {
                muxer.setOrientationHint(videoRotation);
            }

            int muxerVideoTrackIndex = -1;
            int muxerAudioTrackIndex = -1;
            boolean muxerStarted = false;

            if (audioTrackIndex >= 0) {
                try {
                    muxerAudioTrackIndex = muxer.addTrack(inputAudioFormat);
                    audioExtractor.selectTrack(audioTrackIndex);
                } catch (Exception audioTrackEx) {
                    Log.w(TAG, "Audio track could not be added: " + audioTrackEx.getMessage());
                    muxerAudioTrackIndex = -1;
                }
            }

            MediaCodec.BufferInfo bufferInfo = new MediaCodec.BufferInfo();
            boolean isDecoderInputDone = false;
            boolean isDecoderOutputDone = false;
            boolean isEncoderDone = false;
            long lastReportedTimeUs = 0;
            long lastMuxerVideoPts = -1;

            while (!isEncoderDone) {
                // A. Feed Decoder from Extractor
                if (!isDecoderInputDone) {
                    int inputBufIndex = decoder.dequeueInputBuffer(TIMEOUT_USEC);
                    if (inputBufIndex >= 0) {
                        ByteBuffer inputBuf = decoder.getInputBuffer(inputBufIndex);
                        if (inputBuf != null) {
                            int sampleSize = videoExtractor.readSampleData(inputBuf, 0);
                            if (sampleSize < 0) {
                                decoder.queueInputBuffer(inputBufIndex, 0, 0, 0, MediaCodec.BUFFER_FLAG_END_OF_STREAM);
                                isDecoderInputDone = true;
                            } else {
                                decoder.queueInputBuffer(inputBufIndex, 0, sampleSize, videoExtractor.getSampleTime(), videoExtractor.getSampleFlags());
                                videoExtractor.advance();
                            }
                        }
                    }
                }

                // B. Drain Decoder to Encoder Input Surface with exact Nanosecond Timestamp
                if (!isDecoderOutputDone) {
                    int decoderStatus = decoder.dequeueOutputBuffer(bufferInfo, TIMEOUT_USEC);
                    if (decoderStatus >= 0) {
                        if (bufferInfo.size != 0) {
                            // API 23+: release to surface passing nanoseconds presentation timestamp
                            long renderTimestampNs = bufferInfo.presentationTimeUs * 1000L;
                            decoder.releaseOutputBuffer(decoderStatus, renderTimestampNs);
                        } else {
                            decoder.releaseOutputBuffer(decoderStatus, false);
                        }

                        if ((bufferInfo.flags & MediaCodec.BUFFER_FLAG_END_OF_STREAM) != 0) {
                            isDecoderOutputDone = true;
                            encoder.signalEndOfInputStream();
                        }
                    }
                }

                // C. Drain Encoder output to Muxer
                int encoderStatus = encoder.dequeueOutputBuffer(bufferInfo, TIMEOUT_USEC);
                if (encoderStatus == MediaCodec.INFO_OUTPUT_FORMAT_CHANGED) {
                    if (muxerStarted) {
                        throw new RuntimeException("Encoder format changed twice");
                    }
                    MediaFormat newEncoderFormat = encoder.getOutputFormat();
                    muxerVideoTrackIndex = muxer.addTrack(newEncoderFormat);
                    muxer.start();
                    muxerStarted = true;
                } else if (encoderStatus >= 0) {
                    ByteBuffer encodedData = encoder.getOutputBuffer(encoderStatus);
                    if (encodedData != null) {
                        if ((bufferInfo.flags & MediaCodec.BUFFER_FLAG_CODEC_CONFIG) != 0) {
                            bufferInfo.size = 0;
                        }

                        if (bufferInfo.size != 0 && muxerStarted) {
                            if (bufferInfo.presentationTimeUs <= lastMuxerVideoPts) {
                                bufferInfo.presentationTimeUs = lastMuxerVideoPts + 1000;
                            }
                            lastMuxerVideoPts = bufferInfo.presentationTimeUs;

                            encodedData.position(bufferInfo.offset);
                            encodedData.limit(bufferInfo.offset + bufferInfo.size);
                            muxer.writeSampleData(muxerVideoTrackIndex, encodedData, bufferInfo);

                            if (durationUs > 0 && bufferInfo.presentationTimeUs - lastReportedTimeUs > 200000) {
                                lastReportedTimeUs = bufferInfo.presentationTimeUs;
                                float progress = Math.min(1.0f, (float) bufferInfo.presentationTimeUs / (float) durationUs);
                                postProgress(callback, progress);
                            }
                        }

                        encoder.releaseOutputBuffer(encoderStatus, false);

                        if ((bufferInfo.flags & MediaCodec.BUFFER_FLAG_END_OF_STREAM) != 0) {
                            isEncoderDone = true;
                        }
                    }
                }
            }

            // D. Transmux audio track
            if (muxerAudioTrackIndex >= 0 && muxerStarted) {
                try {
                    ByteBuffer audioBuffer = ByteBuffer.allocate(512 * 1024);
                    MediaCodec.BufferInfo audioInfo = new MediaCodec.BufferInfo();
                    long lastAudioPts = -1;

                    while (true) {
                        int sampleSize = audioExtractor.readSampleData(audioBuffer, 0);
                        if (sampleSize < 0) {
                            break;
                        }

                        long sampleTime = audioExtractor.getSampleTime();
                        if (sampleTime <= lastAudioPts) {
                            sampleTime = lastAudioPts + 1000;
                        }
                        lastAudioPts = sampleTime;

                        audioInfo.offset = 0;
                        audioInfo.size = sampleSize;
                        audioInfo.presentationTimeUs = sampleTime;
                        audioInfo.flags = audioExtractor.getSampleFlags();

                        muxer.writeSampleData(muxerAudioTrackIndex, audioBuffer, audioInfo);
                        audioExtractor.advance();
                    }
                } catch (Exception audioEx) {
                    Log.w(TAG, "Audio write warning: " + audioEx.getMessage());
                }
            }

        } finally {
            try { if (decoder != null) { decoder.stop(); decoder.release(); } } catch (Exception ignored) {}
            try { if (encoder != null) { encoder.stop(); encoder.release(); } } catch (Exception ignored) {}
            try { if (muxer != null) { muxer.stop(); muxer.release(); } } catch (Exception ignored) {}
            try { videoExtractor.release(); } catch (Exception ignored) {}
            try { audioExtractor.release(); } catch (Exception ignored) {}
        }

        final long compressedSize = new File(outputPath).length();
        if (compressedSize <= 0) {
            throw new IOException("Compressed video file is 0 bytes");
        }

        postProgress(callback, 1.0f);
        postSuccess(callback, outputPath, originalSize, compressedSize);
    }

    private static void postProgress(final CompressionCallback callback, final float progress) {
        if (callback == null) return;
        new Handler(Looper.getMainLooper()).post(new Runnable() {
            @Override
            public void run() {
                callback.onProgress(progress);
            }
        });
    }

    private static void postSuccess(final CompressionCallback callback, final String path, final long orig, final long comp) {
        if (callback == null) return;
        new Handler(Looper.getMainLooper()).post(new Runnable() {
            @Override
            public void run() {
                callback.onSuccess(path, orig, comp);
            }
        });
    }

    private static void postFailure(final CompressionCallback callback, final String err) {
        if (callback == null) return;
        new Handler(Looper.getMainLooper()).post(new Runnable() {
            @Override
            public void run() {
                callback.onFailure(err);
            }
        });
    }
}
