// Copyright 2022-2025 Niantic.

using System;
using Niantic.Lightship.AR.Utilities;
using Unity.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.MetaQuest.InternalSamples
{
    /// <summary>
    /// This sample demonstrates how to fetch the camera image as a XRCpuImage and convert it to
    /// a RGBA32 texture to be displayed on the UI.
    /// </summary>
    public class CameraDisplayRgba : MonoBehaviour
    {
        private static readonly int s_displayMatrix = Shader.PropertyToID("_DisplayMatrix");
        private static readonly int s_mainTex = Shader.PropertyToID("_MainTex");

        [SerializeField]
        private ARCameraManager _cameraManager;

        [SerializeField]
        private RawImage _rawImage;

        [SerializeField]
        private Material _rgbMaterial;

        // Resources
        private NativeArray<byte> _rgbaBuffer;
        private Texture2D _texture;

        private void OnEnable()
        {
            _cameraManager.frameReceived += FrameReceived;
        }

        private void OnDisable()
        {
            _cameraManager.frameReceived -= FrameReceived;
        }

        private void OnDestroy()
        {
            if (_rgbaBuffer.IsCreated)
            {
                _rgbaBuffer.Dispose();
            }

            if (_texture != null)
            {
                UnityObjectUtils.Destroy(_texture);
            }
        }

        private void FrameReceived(ARCameraFrameEventArgs obj)
        {
            // Acquire the latest cpu image
            if (_cameraManager.TryAcquireLatestCpuImage(out var image))
            {
                // Convert to RGBA32
                UpdateTextureRgba32(image, ref _texture);

                // Release the cpu image
                image.Dispose();

                // Display the image on the UI
                DisplayTexture(_texture);
            }
        }

        /// <summary>
        /// Convert the contents of the specified cpu image to RGBA32 format and copies it to the destination texture.
        /// If the destination texture is null, it will be created.
        /// </summary>
        /// <param name="source">The source XRCpuImage.</param>
        /// <param name="destination">The destination Texture2D</param>
        /// <returns></returns>
        private void UpdateTextureRgba32(XRCpuImage source, ref Texture2D destination)
        {
            // Define conversion params for RGBA32
            var conversionParams = new XRCpuImage.ConversionParams(source, TextureFormat.RGBA32);

            if (destination == null || destination.width != source.width || destination.height != source.height)
            {
                destination = new Texture2D(
                    width: conversionParams.outputDimensions.x,
                    height: conversionParams.outputDimensions.y,
                    textureFormat: conversionParams.outputFormat,
                    mipChain: false
                );
            }

            // Allocate the result buffer
            if (!_rgbaBuffer.IsCreated)
            {
                _rgbaBuffer = new NativeArray<byte>(
                    length: source.GetConvertedDataSize(new Vector2Int(source.width, source.height), TextureFormat.RGBA32),
                    allocator: Allocator.Persistent);
            }

            try
            {
                source.Convert(conversionParams, _rgbaBuffer);
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to convert image: " + e.Message);
            }

            destination.SetPixelData(_rgbaBuffer, 0);
            destination.Apply(false);
        }

        /// <summary>
        /// Displays the provided the texture on the UI.
        /// </summary>
        /// <param name="texture">The texture to display.</param>
        private void DisplayTexture(Texture texture)
        {
            // Verify the texture
            if (texture == null)
            {
                Debug.LogWarning("Texture is null");
                return;
            }

            _rgbMaterial.SetTexture(s_mainTex, texture);
            _rawImage.material = _rgbMaterial;

            // Get the width and height of the viewport
            var rectTransform = _rawImage.rectTransform;
            var viewportWidth = (int)rectTransform.rect.width;
            var viewportHeight = (int)rectTransform.rect.height;

            // Get the camera image and display matrix
            var displayMatrix = CameraMath.CalculateDisplayMatrix(
                    texture.width,
                    texture.height,
                    viewportWidth,
                    viewportHeight,
                    viewportWidth > viewportHeight
                        ? ScreenOrientation.LandscapeLeft
                        : ScreenOrientation.Portrait,
                    layout: CameraMath.MatrixLayout.RowMajor);

            // Set the display matrix for the image
            _rawImage.material.SetMatrix(s_displayMatrix, displayMatrix);
        }
    }
}
