// Copyright 2022-2025 Niantic.

using System;
using Niantic.Lightship.AR.Subsystems.Common;
using Unity.Collections.LowLevel.Unsafe;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.MetaQuest
{
    /// <summary>
    /// Utility class for acquiring cpu image data from external textures created by an XR subsystem.
    /// </summary>
    public abstract class XRTexture : IDisposable
    {
        /// <summary>
        /// The external texture, native to the running device.
        /// </summary>
        public abstract Texture ExternalTexture { get; }

        /// <summary>
        /// The width of the texture.
        /// </summary>
        public abstract int Width { get; }

        /// <summary>
        /// The height of the texture.
        /// </summary>
        public abstract int Height { get; }

        /// <summary>
        /// The format of the external texture.
        /// </summary>
        protected abstract GraphicsFormat Format { get; }

        /// <summary>
        /// A working texture to read pixels from the external texture to cpu.
        /// </summary>
        private Texture2D _tempTexture2D;

        /// <summary>
        /// The timestamp of the image in milliseconds.
        /// </summary>
        protected abstract ulong TimestampMs { get; }

        /// <summary>
        /// Updates the Unity representation of the external texture based on the provided descriptor.
        /// </summary>
        /// <param name="descriptor">The descriptor containing the texture information.</param>
        /// <param name="timestampMs">The timestamp in milliseconds.</param>
        /// <returns>True if the texture was created or updated successfully, false otherwise.</returns>
        public abstract bool Update(XRTextureDescriptor descriptor, ulong timestampMs);

        /// <summary>
        /// Creates an XRTexture instance based on the provided descriptor.
        /// </summary>
        /// <param name="descriptor">The descriptor containing the texture information.</param>
        /// <returns>An instance of XRTexture.</returns>
        public static XRTexture CreateInstance(XRTextureDescriptor descriptor)
        {
            switch (descriptor.textureType)
            {
                case XRTextureType.Texture2D:
                    return new XRTexture2D();

                case XRTextureType.ColorRenderTexture:
                case XRTextureType.DepthRenderTexture:
                    return new XRRenderTexture();

                default:
                    throw new NotSupportedException(
                        $"Unsupported texture type {descriptor.textureType}.");
            }
        }

        public virtual void Dispose()
        {
            if (_tempTexture2D != null)
            {
                UnityObjectUtils.Destroy(_tempTexture2D);
                _tempTexture2D = null;
            }
        }

        /// <summary>
        /// Copies the contents of this XRTexture to the specified Texture2D object.
        /// </summary>
        /// <param name="target">
        /// The target texture to copy to. If the texture is not allocated prior, it will be created.
        /// If the format is not compatible, the texture will be re-created with the format of the source texture.
        /// </param>
        /// <param name="copyMaterial">
        /// The material to use for copying. If null, the format of the target must match the format of the XRTexture.
        /// </param>
        /// <param name="pushToGpu">Whether to push the texture data to the GPU after copying.</param>
        public void Copy(ref Texture2D target, Material copyMaterial = null, bool pushToGpu = true)
        {
            Debug.Assert(ExternalTexture != null);

            var textureFormat = target != null
                ? target.format
                : GraphicsFormatUtility.GetTextureFormat(ExternalTexture.graphicsFormat);

            XRTextureUtils.CopyTextureToTexture2D(ExternalTexture, ref target, textureFormat, copyMaterial, pushToGpu);
        }

        /// <summary>
        /// Tries to acquire the CPU image from this XRTexture.
        /// </summary>
        /// <param name="cinfo">The CInfo to create an XRCpuImage instance from.</param>
        /// <returns>True if the resulting XRCpuImage.Cinfo is valid to use; otherwise, false.</returns>
        public bool TryAcquireCpuImage(out XRCpuImage.Cinfo cinfo)
        {
            // Is the source texture valid?
            if (ExternalTexture == null)
            {
                cinfo = default;
                return false;
            }

            // Determine the nature of the external texture
            switch (ExternalTexture)
            {
                case RenderTexture renderTexture:
                {
                    // If the texture is GPU only, we need to read back to CPU.
                    // Here we don't need to use an intermediate texture.
                    XRTextureUtils.CopyRenderTextureToTexture2D(renderTexture, ref _tempTexture2D, pushToGpu: false);
                    break;
                }

                case Texture2D texture2D:
                {
                    // Unfortunately, we have to copy here because Unity cannot
                    // access the data of an external texture directly. This API
                    // uses an intermediate texture.
                    XRTextureUtils.CopyTextureToTexture2D(texture2D, ref _tempTexture2D, pushToGpu: false);
                    break;
                }

                default:
                    throw new NotSupportedException(
                        $"Unsupported texture type {ExternalTexture.GetType()}.");
            }

            // Copy the CPU texture data to the XRCpuImage
            return CopyTextureToCpuImage(_tempTexture2D, out cinfo);
        }

        /// <summary>
        /// Copies the pixels of the source cpu texture to the lightship cpu image data repository.
        /// </summary>
        private unsafe bool CopyTextureToCpuImage(Texture2D source, out XRCpuImage.Cinfo result)
        {
            var data = source.GetPixelData<byte>(0);
            return LightshipCpuImageApi.Instance.TryAddManagedXRCpuImage((IntPtr)data.GetUnsafeReadOnlyPtr(),
                data.Length, source.width, source.height, source.format, TimestampMs, out result);
        }
    }
}
