// Copyright 2022-2025 Niantic.
using System.Linq;
using Niantic.Lightship.AR.Semantics;
using UnityEngine;
using UnityEngine.UI;

namespace Niantic.Lightship.MetaQuest.InternalSamples
{
    public class FilterSemantics : MonoBehaviour
    {
        [SerializeField]
        private ARSemanticSegmentationManager _semanticSegmentationManager;

        [SerializeField]
        private LightshipSemanticsOverlay _semanticsOverlay;

        [SerializeField]
        private Dropdown _channelDropdown;

        // Display ground by default
        private const int DefaultChannel = 3;

        private void OnEnable()
        {
            _semanticSegmentationManager.MetadataInitialized += OnMetadataInitialized;
            _channelDropdown.onValueChanged.AddListener(OnChannelDropdownValueChanged);
        }

        private void OnDisable()
        {
            _semanticSegmentationManager.MetadataInitialized -= OnMetadataInitialized;
            _channelDropdown.onValueChanged.RemoveListener(OnChannelDropdownValueChanged);
        }

        private void OnChannelDropdownValueChanged(int val)
        {
            _semanticsOverlay.SetChannel(val);
        }

        private void OnMetadataInitialized(ARSemanticSegmentationModelEventArgs obj)
        {
            // Initialize the channel names in the dropdown menu.
            var channelNames = _semanticSegmentationManager.ChannelNames;

            // Set the default channel.
            _semanticsOverlay.SetChannel(DefaultChannel);

            if (_channelDropdown is not null)
            {
                _channelDropdown.AddOptions(channelNames.ToList());
                _channelDropdown.value = DefaultChannel;
            }
        }
    }
}
