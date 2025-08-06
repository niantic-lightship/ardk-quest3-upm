using System.Linq;
using Niantic.Lightship.AR.Semantics;
using Niantic.Lightship.AR.Utilities;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.MetaQuest.InternalSamples
{
    public sealed class SemanticsImageDisplay : MonoBehaviour
    {
        /// <summary>
        /// The shader property ID for the display matrix.
        /// </summary>
        private static readonly int s_displayMatrix = Shader.PropertyToID("_DisplayMatrix");

        [SerializeField]
        private ARSemanticSegmentationManager _semanticSegmentationManager;

        [SerializeField]
        private RawImage _rawImage;

        [SerializeField]
        private Material _material;

        [SerializeField]
        private Text _imageInfoText;

        [SerializeField]
        private Dropdown _channelDropdown;

        // The name of the currently selected semantic channel
        private string _semanticChannelName = string.Empty;

        private void OnEnable()
        {
            _semanticSegmentationManager.MetadataInitialized += OnSemanticsMetadataInitialized;
            _channelDropdown.onValueChanged.AddListener(OnChanelDropdownValueChanged);
        }

        private void OnDisable()
        {
            _semanticSegmentationManager.MetadataInitialized -= OnSemanticsMetadataInitialized;
            _channelDropdown.onValueChanged.RemoveListener(OnChanelDropdownValueChanged);
        }

        private void Update()
        {
            // Get the width and height of the viewport
            var rectTransform = _rawImage.rectTransform;
            var viewportWidth = (int)rectTransform.rect.width;
            var viewportHeight = (int)rectTransform.rect.height;

            // Use the XRCameraParams type to describe the viewport to fit the semantics image to
            var viewport = new XRCameraParams
            {
                screenWidth = viewportWidth, screenHeight = viewportHeight, screenOrientation = XRDisplayContext.GetScreenOrientation()
            };

            // Acquire the texture with the confidence values of the currently selected channel
            var image = _semanticSegmentationManager.GetSemanticChannelTexture(_semanticChannelName, out var displayMatrix, viewport);
            if (image != null)
            {
                // Update the texture and material of the RawImage component
                _rawImage.texture = image;
                _rawImage.material = _material;
                _rawImage.material.SetMatrix(s_displayMatrix, displayMatrix);

                // Log the image information
                _imageInfoText.text = $"{_semanticChannelName}" + $"\nWidth: {image.width}" + $"\nHeight: {image.height}" +
                    $"\nFormat: {image.format}";
            }
            else
            {
                _rawImage.texture = null;
                _imageInfoText.text = "No image available";
            }
        }

        /// <summary>
        /// Callback for when the semantic metadata (e.g. channel list) becomes available.
        /// </summary>
        private void OnSemanticsMetadataInitialized(ARSemanticSegmentationModelEventArgs args)
        {
            // Initialize the channel names in the dropdown menu.
            var channelNames = _semanticSegmentationManager.ChannelNames;

            // Display artificial ground by default.
            _semanticChannelName = channelNames[3];

            if (_channelDropdown is not null)
            {
                _channelDropdown.AddOptions(channelNames.ToList());

                var dropdownList = _channelDropdown.options.Select(option => option.text).ToList();
                _channelDropdown.value = dropdownList.IndexOf(_semanticChannelName);
            }
        }

        /// <summary>
        /// Callback for when the semantic channel dropdown UI has a value change.
        /// </summary>
        private void OnChanelDropdownValueChanged(int val)
        {
            // Update the display channel from the dropdown value.
            _semanticChannelName = _channelDropdown.options[val].text;
        }
    }
}
