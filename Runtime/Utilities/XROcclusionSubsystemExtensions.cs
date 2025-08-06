// Copyright 2022-2025 Niantic.

using System;
using Niantic.Lightship.AR.Subsystems.Common;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.OpenXR.Features.Meta;

namespace Niantic.Lightship.MetaQuest
{
    public static class XROcclusionSubsystemExtensions
    {
        static XROcclusionSubsystemExtensions()
        {
            // Register events to release resources
            SceneManager.activeSceneChanged += SceneManager_OnActiveSceneChanged;
            Application.quitting += Application_OnQuitting;
        }

        private static void Application_OnQuitting() => ReleaseResources();
        private static void SceneManager_OnActiveSceneChanged(Scene arg0, Scene arg1) => ReleaseResources();

        // Resources
        private static Material s_unpackDepthMeta;
        private static XRTexture s_xrTexture;

        /// <summary>
        /// Returns the blit material for copying depth data.
        /// </summary>
        /// <param name="forSubsystem"></param>
        /// <returns></returns>
        private static Material GetBlitMaterial(XROcclusionSubsystem forSubsystem)
        {
            switch (forSubsystem)
            {
                case MetaOpenXROcclusionSubsystem:
                {
                    if (s_unpackDepthMeta == null)
                    {
                        // Writes the first entry of a texture array to a texture2D. Used on the Meta Quest platform.
                        var unpackDepthShader = Shader.Find("Hidden/UnpackDepth");
                        if (unpackDepthShader != null)
                        {
                            s_unpackDepthMeta = new Material(unpackDepthShader)
                            {
                                name = "UnpackDepth", hideFlags = HideFlags.HideAndDontSave
                            };
                        }
                        else
                        {
                            Debug.LogError(
                                "The shader 'Hidden/UnpackDepth' was not found. Ensure that the shader is included in the build.");
                        }
                    }

                    return s_unpackDepthMeta;
                }

                default:
                    return null;
            }

        }

        /// <summary>
        /// Tries to get the environment depth as a Texture2D from the occlusion subsystem.
        /// </summary>
        /// <param name="occlusionSubsystem">The occlusion subsystem to get the depth from.</param>
        /// <param name="destination">
        /// The destination texture to write the depth data to. This texture may get destroyed and
        /// recreated if the size or format does not match the depth data.
        /// </param>
        /// <param name="pushToGpu">If true, the data will be copied to gpu memory via Texture2D.Apply().</param>
        /// <returns>True if the depth data was successfully retrieved, false otherwise.</returns>
        public static bool TryGetEnvironmentDepthExt
        (
            this XROcclusionSubsystem occlusionSubsystem,
            ref Texture2D destination,
            bool pushToGpu = true)
        {
            if (occlusionSubsystem.TryGetEnvironmentDepth(out var descriptor))
            {
                // Update the texture with the new descriptor
                s_xrTexture ??= XRTexture.CreateInstance(descriptor);
                if (s_xrTexture.Update(descriptor, 0u))
                {
                    // Re-create the destination texture if needed
                    CheckTexture(ref destination, s_xrTexture.Width, s_xrTexture.Height, TextureFormat.RFloat);

                    // Copy the data from the external texture
                    s_xrTexture.Copy(ref destination, GetBlitMaterial(occlusionSubsystem), pushToGpu);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Tries to acquire the environment depth as a CPU image from the occlusion subsystem,
        /// even if the occlusion subsystem does not implement CPU images.
        /// </summary>
        /// <param name="occlusionSubsystem"></param>
        /// <param name="tempDataHolder">
        /// If the subsystem does not implement CPU images, this texture will be used as
        /// a temporary data holder to copy GPU depth to CPU memory. This texture may get
        /// destroyed and recreated if the size or format does not match the depth data.
        /// </param>
        /// <param name="cpuImage">The resulting CPU image.</param>
        /// <returns>True if the CPU image was successfully acquired, false otherwise.</returns>
        public static bool TryAcquireEnvironmentDepthCpuImageExt
        (
            this XROcclusionSubsystem occlusionSubsystem,
            ref Texture2D tempDataHolder,
            out XRCpuImage cpuImage)
        {
            try
            {
                // Use the first-class implementation if possible
                if (occlusionSubsystem.TryAcquireEnvironmentDepthCpuImage(out cpuImage))
                {
                    return true;
                }
            }
            // Silence the NotSupportedException
            catch (NotSupportedException)
            {
            }

            // Get the depth as an internal texture
            if (occlusionSubsystem.TryGetEnvironmentDepthExt(ref tempDataHolder, pushToGpu: false))
            {
                bool didCopyData;
                XRCpuImage.Cinfo cinfo;
                var api = LightshipCpuImageApi.Instance;
                unsafe
                {
                    // Copy the texture data to the cpu image data repository
                    var data = tempDataHolder.GetPixelData<byte>(0);
                    didCopyData = api.TryAddManagedXRCpuImage
                    (
                        (IntPtr)data.GetUnsafeReadOnlyPtr(),
                        data.Length,
                        tempDataHolder.width,
                        tempDataHolder.height,
                        tempDataHolder.format,
                        GetCurrentTimestampMs(),
                        out cinfo);
                }

                // If the copy was successful, create the cpu image
                if (didCopyData)
                {
                    cpuImage = new XRCpuImage(api, cinfo);
                    return true;
                }
            }

            cpuImage = default;
            return false;
        }

        /// <summary>
        /// Checks if the texture is valid and re-initializes it if needed.
        /// </summary>
        private static void CheckTexture(ref Texture2D texture, int targetWidth, int targetHeight,
            TextureFormat targetFormat)
        {
            if (texture == null)
            {
                texture = new Texture2D(targetWidth, targetHeight, targetFormat, false);
            }
            else if (texture.width != targetWidth || texture.height != targetHeight || texture.format != targetFormat)
            {
                texture.Reinitialize(targetWidth, targetHeight, targetFormat, false);
            }
        }

        /// <summary>
        /// Returns the current application timestamp in milliseconds.
        /// </summary>
        private static ulong GetCurrentTimestampMs() => (ulong)(Time.unscaledTime * 1000.0f);

        /// <summary>
        /// Call this to clean up residual resources that reside in the cache due to the usage of extension methods.
        /// <remarks>Automatically called when the application quits or the active scene changes.</remarks>
        /// </summary>
        public static void ReleaseResources()
        {
            if (s_unpackDepthMeta != null)
            {
                UnityEngine.Object.Destroy(s_unpackDepthMeta);
                s_unpackDepthMeta = null;
            }

            if (s_xrTexture != null)
            {
                s_xrTexture.Dispose();
                s_xrTexture = null;
            }
        }
    }
}
