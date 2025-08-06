// Copyright 2022-2025 Niantic.

using System;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.XR;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR.API;

namespace Niantic.Lightship.MetaQuest
{
    internal sealed class XRRenderTexture : XRTexture
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
            get => GraphicsFormatUtility.GetGraphicsFormat(_descriptor.format,
                    isSRGB: _descriptor.textureType is XRTextureType.ColorRenderTexture);
        }

        // Resources
        private RenderTexture _texture;
        private XRTextureDescriptor _descriptor;

        // State
        private ulong _timestampMs;
        private uint _renderTextureId;
        private bool _isCreateRequested;
        private bool _isCreated;

        /// <summary>
        /// Updates the Unity representation of the external texture based on the provided descriptor.
        /// </summary>
        /// <param name="descriptor">The descriptor containing the texture information.</param>
        /// <param name="timestampMs">The timestamp in milliseconds.</param>
        /// <returns>True if the texture was created or updated successfully, false otherwise.</returns>
        public override bool Update(XRTextureDescriptor descriptor, ulong timestampMs)
        {
            // Check if the descriptor is valid and matches the expected texture type
            if (descriptor.textureType != XRTextureType.DepthRenderTexture &&
                descriptor.textureType != XRTextureType.ColorRenderTexture)
            {
                throw new ArgumentException(
                    $"Invalid texture type {descriptor.textureType}.");
            }

            // Check if the descriptor is valid
            if (!descriptor.valid)
            {
                Debug.LogWarning($"Invalid texture descriptor: {descriptor}");
                return false;
            }

            // Determine the nature of this update
            var newTexture = !_descriptor.hasIdenticalTextureMetadata(descriptor);
            var differentTexture = _descriptor.propertyNameId != descriptor.propertyNameId;
            var newAllocation = _descriptor.nativeTexture != descriptor.nativeTexture;

            // If the texture has been created
            if (_isCreated)
            {
                // Does the texture need to be recreated?
                if (newTexture || newAllocation)
                {
                    // Re-allocate
                    ReleaseTexture();
                    RequestCreateTexture(descriptor);

                    // Update the timestamp
                    _timestampMs = timestampMs;

                    return TryRetrieveTexture();
                }

                // The texture is still valid, but the property name has changed
                if (differentTexture)
                {
                    // Update the property name
                    _descriptor = descriptor;
                }

                return true;
            }

            if (!_isCreated && !_isCreateRequested)
            {
                // Allocate
                RequestCreateTexture(descriptor);

                // Set the image timestamp
                _timestampMs = timestampMs;
            }

            if (!_isCreated)
            {
                return TryRetrieveTexture();
            }

            return false;
        }

        private void RequestCreateTexture(XRTextureDescriptor newDescriptor)
        {
            var displaySubsystem = DisplaySubsystem;
            if (displaySubsystem == null)
            {
                Debug.LogError("RenderTexture cannot be created because the XRDisplaySubsystem is not loaded.");
                return;
            }

            if (UnityXRDisplay.CreateTexture(XRTextureUtils.ToUnityXRRenderTextureDesc(newDescriptor), out _renderTextureId))
            {
                _isCreateRequested = true;
                _descriptor = newDescriptor;
            }
            else
            {
                Debug.LogError($"Failed to create texture from descriptor {_descriptor}");
            }
        }

        private bool TryRetrieveTexture()
        {
            var displaySubsystem = DisplaySubsystem;
            if (displaySubsystem == null)
            {
                Debug.LogError("RenderTexture cannot be retrieved because the XRDisplaySubsystem is not loaded.");
                return false;
            }

            _texture = displaySubsystem.GetRenderTexture(_renderTextureId);
            if (_texture != null)
            {
                _isCreated = true;
            }

            return _isCreated;
        }

        private void ReleaseTexture()
        {
            UnityObjectUtils.Destroy(_texture);
            _texture = null;
            _isCreated = false;
            _isCreateRequested = false;
        }

        public override void Dispose()
        {
            base.Dispose();
            ReleaseTexture();
            _descriptor = default;
        }

        /// <summary>
        /// Retrieves the XRDisplaySubsystem instance, if available.
        /// </summary>
        private static XRDisplaySubsystem DisplaySubsystem
        {
            get
            {
                if (XRGeneralSettings.Instance == null || XRGeneralSettings.Instance.Manager == null)
                {
                    return null;
                }

                var loader = XRGeneralSettings.Instance.Manager.activeLoader;
                return loader != null ? loader.GetLoadedSubsystem<XRDisplaySubsystem>() : null;
            }
        }
    }
}
