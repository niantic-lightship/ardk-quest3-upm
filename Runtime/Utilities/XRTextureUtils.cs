// Copyright 2022-2025 Niantic.

using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.OpenXR.API;

namespace Niantic.Lightship.MetaQuest
{
    internal static class XRTextureUtils
    {
        internal static UnityXRRenderTextureFormat ToUnityXRRenderTextureFormat(TextureFormat textureFormat)
        {
            switch (textureFormat)
            {
                case TextureFormat.RGBA32:
                    return UnityXRRenderTextureFormat.kUnityXRRenderTextureFormatRGBA32;
                case TextureFormat.BGRA32:
                    return UnityXRRenderTextureFormat.kUnityXRRenderTextureFormatBGRA32;
                case TextureFormat.RGB565:
                    return UnityXRRenderTextureFormat.kUnityXRRenderTextureFormatRGB565;
                case TextureFormat.RGBAHalf:
                    return UnityXRRenderTextureFormat.kUnityXRRenderTextureFormatR16G16B16A16_SFloat;
                default:
                    throw new NotSupportedException(
                        $"Attempted to convert unsupported TextureFormat {textureFormat} to UnityXRRenderTextureFormat");
            }
        }

        internal static UnityXRDepthTextureFormat ToUnityXRDepthTextureFormat(TextureFormat textureFormat)
        {
            switch (textureFormat)
            {
                case TextureFormat.RFloat:
                    return UnityXRDepthTextureFormat.kUnityXRDepthTextureFormat24bitOrGreater;
                case TextureFormat.R16:
                case TextureFormat.RHalf:
                    return UnityXRDepthTextureFormat.kUnityXRDepthTextureFormat16bit;
                default:
                    throw new NotSupportedException(
                        $"Attempted to convert unsupported TextureFormat {textureFormat} to UnityXRDepthTextureFormat");
            }
        }

        internal static UnityXRRenderTextureDesc ToUnityXRRenderTextureDesc(XRTextureDescriptor descriptor)
        {
            var renderTextureDescriptor = new UnityXRRenderTextureDesc
            {
                shadingRateFormat = UnityXRShadingRateFormat.kUnityXRShadingRateFormatNone,
                shadingRate = new UnityXRTextureData(),
                width = (uint)descriptor.width,
                height = (uint)descriptor.height,
                textureArrayLength = (uint)descriptor.depth,
                flags = 0,
                colorFormat = UnityXRRenderTextureFormat.kUnityXRRenderTextureFormatNone,
                depthFormat = UnityXRDepthTextureFormat.kUnityXRDepthTextureFormatNone
            };

            switch (descriptor.textureType)
            {
                case XRTextureType.DepthRenderTexture:
                    renderTextureDescriptor.depthFormat = ToUnityXRDepthTextureFormat(descriptor.format);
                    renderTextureDescriptor.depth = new UnityXRTextureData
                    {
                        nativePtr = descriptor.nativeTexture
                    };
                    break;
                case XRTextureType.ColorRenderTexture:
                    renderTextureDescriptor.colorFormat = ToUnityXRRenderTextureFormat(descriptor.format);
                    renderTextureDescriptor.color = new UnityXRTextureData
                    {
                        nativePtr = descriptor.nativeTexture
                    };
                    break;
            }

            return renderTextureDescriptor;
        }

        /// <summary>
        /// Copies the pixels of the source gpu texture to the destination cpu texture.
        /// </summary>
        /// <param name="source">The source render texture.</param>
        /// <param name="destination">The destination texture. If not created by the caller, it will be created.</param>
        /// <param name="pushToGpu">Whether to push the texture data to the GPU after copying.</param>
        internal static void CopyRenderTextureToTexture2D(
            RenderTexture source,
            ref Texture2D destination,
            bool pushToGpu = false)
        {
            var width = source.width;
            var height = source.height;
            var format = GraphicsFormatUtility.GetGraphicsFormat(source.format, source.sRGB);

            if (destination == null)
            {
                destination = new Texture2D(width, height, format, TextureCreationFlags.DontInitializePixels);
            }
            else if (destination.width != width || destination.height != height || destination.graphicsFormat != format)
            {
                destination.Reinitialize(width, height, format, false);
            }

            var previousActive = RenderTexture.active;
            RenderTexture.active = source;
            destination.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            RenderTexture.active = previousActive;

            if (pushToGpu)
            {
                destination.Apply(false, false);
            }
        }

        /// <summary>
        /// Copies the source texture to the destination texture.
        /// </summary>
        /// <param name="source">The source texture.</param>
        /// <param name="destination">The destination texture. If not created by the caller, it will be created.</param>
        /// <param name="mirrorX">Whether to mirror the image horizontally.</param>
        /// <param name="pushToGpu">Whether to push the texture data to the GPU after copying.</param>
        internal static void CopyTextureToTexture2D(
            Texture source,
            ref Texture2D destination,
            bool mirrorX = false,
            bool pushToGpu = false)
        {
            var width = source.width;
            var height = source.height;
            var scale = new Vector2(1, mirrorX ? -1 : 1);
            var offset = new Vector2(0, mirrorX ? 1 : 0);
            var format = source.graphicsFormat;

            if (destination == null)
            {
                destination = new Texture2D(width, height, format, TextureCreationFlags.DontInitializePixels);
            }
            else if (destination.width != width || destination.height != height || destination.graphicsFormat != format)
            {
                destination.Reinitialize(width, height, format, false);
            }

            var rtFormat = GraphicsFormatUtility.GetRenderTextureFormat(format);
            var previousActive = RenderTexture.active;
            var rt = RenderTexture.GetTemporary(width, height, 0, rtFormat);
            Graphics.Blit(source, rt, scale, offset);
            destination.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            RenderTexture.ReleaseTemporary(rt);
            RenderTexture.active = previousActive;

            if (pushToGpu)
            {
                destination.Apply(false, false);
            }
        }

        /// <summary>
        /// Copies the source texture to the destination texture while applying a material.
        /// </summary>
        /// <param name="source">The source texture.</param>
        /// <param name="destination">The destination texture. If not created by the caller, it will be created.</param>
        /// <param name="destinationFormat">The format of the destination texture.</param>
        /// <param name="copyMaterial">The material to apply during the copy.</param>
        /// <param name="pushToGpu">Whether to push the texture data to the GPU after copying.</param>
        internal static void CopyTextureToTexture2D(
            Texture source,
            ref Texture2D destination,
            TextureFormat destinationFormat,
            Material copyMaterial = null,
            bool pushToGpu = false)
        {
            var width = source.width;
            var height = source.height;

            if (destination == null)
            {
                destination = new Texture2D(width, height, destinationFormat, false);
            }
            else if (destination.width != width || destination.height != height || destination.format!= destinationFormat)
            {
                destination.Reinitialize(width, height, destinationFormat, false);
            }

            // Cache the previous active render texture
            var previousActive = RenderTexture.active;

            // Get a temporary render texture with the specified output format
            var rtFormat = GraphicsFormatUtility.GetRenderTextureFormat(destination.graphicsFormat);
            var rt = RenderTexture.GetTemporary(width, height, 0, rtFormat);

            // Perform the blit operation with the material
            if (copyMaterial == null)
            {
                // If no material is provided, use the default blit
                Graphics.Blit(source, rt);
            }
            else
            {
                // Use the provided material for the blit
                copyMaterial.SetTexture("_MainTex", source);
                Graphics.Blit(source, rt, copyMaterial);
            }

            // Read the pixels from the temp texture into the destination texture
            destination.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);

            // Release the temporary render texture
            RenderTexture.ReleaseTemporary(rt);

            // Restore the previous active render texture
            RenderTexture.active = previousActive;

            if (pushToGpu)
            {
                destination.Apply(false, false);
            }
        }
    }
}
