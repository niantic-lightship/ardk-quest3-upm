// Copyright 2022-2025 Niantic.

using Niantic.Lightship.AR.Utilities;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

namespace Niantic.Lightship.MetaQuest.InternalSamples
{
    /// <summary>
    /// This example demonstrates how to acquire and display the depth texture on the UI.
    /// </summary>
    public sealed class GPUImageExample : MonoBehaviour
    {
        /// <summary>
        /// The shader property ID for the display matrix.
        /// </summary>
        private static readonly int s_displayMatrix = Shader.PropertyToID("_DisplayMatrix");

        [SerializeField]
        private AROcclusionManager _occlusionManager;

        [SerializeField]
        private RawImage _rawImage;

        [SerializeField]
        private Material _material;

        [SerializeField]
        private Text _imageInfoText;

        private void Start()
        {
            // Defaults
            _rawImage.texture = null;
            _imageInfoText.text = "No image available";

            // Assign the material that visualizes the depth texture
            _rawImage.material = _material;
        }

        private void OnEnable()
        {
            _occlusionManager.frameReceived += OnFrameReceived;
        }

        private void OnDisable()
        {
            _occlusionManager.frameReceived -= OnFrameReceived;
        }

        private void OnFrameReceived(AROcclusionFrameEventArgs args)
        {
            if (args.externalTextures.Count <= 0)
            {
                return;
            }

            // Get a reference to the depth texture
            var texture = args.externalTextures[0].texture;

            // Get the width and height of the viewport
            var rectTransform = _rawImage.rectTransform;
            var viewportWidth = (int)rectTransform.rect.width;
            var viewportHeight = (int)rectTransform.rect.height;
            var viewportOrientation = XRDisplayContext.GetScreenOrientation();

            // Calculate the display matrix for rendering the depth texture
            // on the RawImage UI element. The texture on Meta Quest usually
            // has a square resolution, so we use AffineMath directly here,
            // instead of CameraMath.CalculateDisplayMatrix to be able to
            // specify the image orientation.
            var displayMatrix = AffineMath.Fit(
                texture.width,
                texture.height,
                ScreenOrientation.LandscapeLeft,
                viewportWidth,
                viewportHeight,
                viewportOrientation).transpose;

            // Update the texture and material of the RawImage component
            _rawImage.texture = texture;
            _rawImage.material.SetMatrix(s_displayMatrix, displayMatrix);

            // Log the image information
            _imageInfoText.text = $"Width: {texture.width}" + $"\nHeight: {texture.height}"
                + $"\nFormat: {texture.graphicsFormat}" + $"\nGpu only: {texture is RenderTexture}"
                + $"\nOrientation: {viewportOrientation}";
        }
    }
}
