using Niantic.Lightship.AR.Utilities;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

namespace Niantic.Lightship.MetaQuest.InternalSamples
{
    public class CameraImageDisplay : MonoBehaviour
    {
        private static readonly int s_displayMatrix = Shader.PropertyToID("_DisplayMatrix");

        [SerializeField]
        private ARCameraManager _cameraManager;

        [SerializeField]
        private RawImage _rawImage;

        [SerializeField]
        private Material _rgbMaterial;

        [SerializeField]
        private Material _yuvMaterial;

        [SerializeField]
        private Text _imageInfoText;

        [SerializeField]
        [Tooltip("Use the dimensions of the raw image UI element to calculate the display matrix")]
        private bool _useUIDimensions = true;

        private void OnEnable()
        {
            _cameraManager.frameReceived += OnFrameReceived;
        }

        private void OnDisable()
        {
            _cameraManager.frameReceived -= OnFrameReceived;
        }

        private void OnFrameReceived(ARCameraFrameEventArgs args)
        {
            switch (args.textures.Count)
            {
                case 1:
                    // Assuming RGB format
                    _rgbMaterial.SetTexture("_MainTex", args.textures[0]);
                    _rawImage.material = _rgbMaterial;
                    break;

                case 2:
                    // Assuming YUV format
                    _yuvMaterial.SetTexture("_TextureY", args.textures[0]);
                    _yuvMaterial.SetTexture("_TextureUV", args.textures[1]);
                    _rawImage.material = _yuvMaterial;
                    break;

                default:
                    return;
            }

            // Get the width and height of the viewport
            var rectTransform = _rawImage.rectTransform;
            var viewportWidth = (int)rectTransform.rect.width;
            var viewportHeight = (int)rectTransform.rect.height;

            // Get the camera image and display matrix
            var cameraImage = args.textures[0];
            var displayMatrix = _useUIDimensions
                ? CameraMath.CalculateDisplayMatrix(
                    cameraImage.width,
                    cameraImage.height,
                    viewportWidth,
                    viewportHeight,
                    viewportWidth > viewportHeight
                        ? ScreenOrientation.LandscapeLeft
                        : ScreenOrientation.Portrait,
                    layout: CameraMath.MatrixLayout.RowMajor)
                : args.displayMatrix ?? Matrix4x4.identity;

            // Set the display matrix for the image
            _rawImage.material.SetMatrix(s_displayMatrix, displayMatrix);

            if (_imageInfoText != null)
            {
                _imageInfoText.text = $"Texture size: {cameraImage.width}x{cameraImage.height}";
                _imageInfoText.text += "\nFormat: " + (args.textures.Count > 1
                    ? "YUV"
                    : args.textures[0].format.ToString());

                if (_useUIDimensions)
                {
                    _imageInfoText.text += $"\nViewport size: {viewportWidth}x{viewportHeight}";
                }
            }
        }
    }
}
