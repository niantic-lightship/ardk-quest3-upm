// Copyright 2022-2025 Niantic.

using System;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.MetaQuest
{
    internal sealed class XRTexture2D : XRTexture
    {
        /// <summary>
        /// The external texture, native to the running device.
        /// </summary>
        public override Texture ExternalTexture => _texture;

        /// <summary>
        /// The width of the texture.
        /// </summary>
        public override int Width => _descriptor.width;

        /// <summary>
        /// The height of the texture.
        /// </summary>
        public override int Height => _descriptor.height;

        /// <summary>
        /// The timestamp of the image in milliseconds.
        /// </summary>
        protected override ulong TimestampMs => _timestampMs;

        /// <summary>
        /// The format of the external texture.
        /// </summary>
        protected override GraphicsFormat Format
        {
            get => GraphicsFormatUtility.GetGraphicsFormat(_descriptor.format, !TextureHasLinearColorSpace);
        }

        // Resources
        private Texture2D _texture;
        private XRTextureDescriptor _descriptor;

        // Helpers
        private const bool TextureHasLinearColorSpace = false;
        private ulong _timestampMs;

        /// <summary>
        /// Updates the Unity representation of the external texture based on the provided descriptor.
        /// </summary>
        /// <param name="descriptor">The descriptor containing the texture information.</param>
        /// <param name="timestampMs">The timestamp in milliseconds.</param>
        /// <returns>True if the texture was created or updated successfully, false otherwise.</returns>
        public override bool Update(XRTextureDescriptor descriptor, ulong timestampMs)
        {
            // Check if the descriptor is valid and matches the expected texture type
            if (descriptor.textureType != XRTextureType.Texture2D)
            {
                throw new ArgumentException(
                    $"Invalid texture type {descriptor.textureType}. Expected Texture2D.");
            }

            // Check if the descriptor is valid
            if (!descriptor.valid)
            {
                Debug.LogWarning($"Invalid texture descriptor: {descriptor}");
                return false;
            }

            // Check if the descriptor has changed
            if (_descriptor == descriptor)
            {
                return true;
            }

            // Update the image timestamp
            _timestampMs = timestampMs;

            // Update texture data if the descriptor has changed
            if (_descriptor.hasIdenticalTextureMetadata(descriptor))
            {
                Debug.Assert(_texture != null);

                _texture.UpdateExternalTexture(descriptor.nativeTexture);
                _descriptor = descriptor;
                return true;
            }

            // The texture needs to be recreated
            if (_texture != null)
            {
                UnityObjectUtils.Destroy(_texture);
            }

            _texture = CreateTextureFromDescriptor(descriptor);

            // NB: SetWrapMode needs to be the first call here, and the value passed
            //     needs to be kTexWrapClamp - this is due to limitations of what
            //     wrap modes are allowed for external textures in OpenGL (which are
            //     used for ARCore), as Texture::ApplySettings will eventually hit
            //     an assert about an invalid enum (see calls to glTexParameteri
            //     towards the top of ApiGLES::TextureSampler)
            // reference: "3.7.14 External Textures" section of
            // https://www.khronos.org/registry/OpenGL/extensions/OES/OES_EGL_image_external.txt
            // (it shouldn't ever matter what the wrap mode is set to normally, since
            // this is for a pass-through video texture, so we shouldn't ever need to
            // worry about the wrap mode as textures should never "wrap")
            _texture.wrapMode = TextureWrapMode.Clamp;
            _texture.filterMode = FilterMode.Bilinear;
            _texture.hideFlags = HideFlags.HideAndDontSave;

            _descriptor = descriptor;
            return true;
        }

        public override void Dispose()
        {
            base.Dispose();
            if (_texture != null)
            {
                UnityObjectUtils.Destroy(_texture);
            }

            _descriptor = default;
        }

        private static Texture2D CreateTextureFromDescriptor(XRTextureDescriptor descriptor)
        {
            return Texture2D.CreateExternalTexture(
                width: descriptor.width,
                height: descriptor.height,
                format: descriptor.format,
                mipChain: descriptor.mipmapCount > 1,
                linear: TextureHasLinearColorSpace,
                nativeTex: descriptor.nativeTexture);
        }
    }
}
