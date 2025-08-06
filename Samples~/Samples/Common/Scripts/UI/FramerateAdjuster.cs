// Copyright 2022-2024 Niantic.
using System;
using Niantic.Lightship.AR.Meshing;
using Niantic.Lightship.AR.ObjectDetection;
using Niantic.Lightship.AR.Occlusion;
using Niantic.Lightship.AR.Semantics;
using Niantic.Lightship.AR.Subsystems.Occlusion;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;

namespace Niantic.Lightship.MetaQuest.InternalSamples
{
    public class FramerateAdjuster : MonoBehaviour
    {
        private const string DisplayFormat = "{0} Target Framerate: {1}";
        private const string DepthLabel = "Depth";
        private const string SemanticsLabel = "Semantics";
        private const string MeshingLabel = "Meshing";
        private const string ObjectDetectionLabel = "ObjectDetection";

        [SerializeField]
        private GameObject _framerateSliderCanvasObject;

        [Header("Occlusion")]
        [SerializeField]
        private LightshipOcclusionExtension _occlusionExtension;

        [SerializeField]
        private Slider _depthFramerateSlider;

        [SerializeField]
        private Text _depthFpsText;

        [Header("Semantic Segmentation")]
        [SerializeField]
        private ARSemanticSegmentationManager _segmentationManager;

        [SerializeField]
        private Slider _segmentationFramerateSlider;

        [SerializeField]
        private Text _segmentationFpsText;

        [Header("Meshing")]
        [SerializeField]
        private LightshipMeshingExtension _meshingExtension;

        [SerializeField]
        private Slider _meshingFramerateSlider;

        [SerializeField]
        private Text _meshingFpsText;

        [Header("Object Detection")]
        [SerializeField]
        private ARObjectDetectionManager _objectDetectionManager;

        [SerializeField]
        private Slider _objectDetectionFramerateSlider;

        [SerializeField]
        private Text _objectDetectionFpsText;

        private bool _isUsingLightshipDepth;

        private void Start()
        {
            // Check if we're using Lightship depth
            var xrManager = XRGeneralSettings.Instance.Manager;
            var occlusionSubsystem = xrManager.activeLoader.GetLoadedSubsystem<XROcclusionSubsystem>();
            _isUsingLightshipDepth = occlusionSubsystem is LightshipOcclusionSubsystem;

            // Set all the framerate sliders to listen to values if they're corresponding extensions are not null
            if (_depthFramerateSlider is not null)
            {
                _depthFramerateSlider.transform.parent.gameObject.SetActive(_occlusionExtension is not null);
                if (_occlusionExtension is not null)
                {
                    _depthFramerateSlider.value = _occlusionExtension.TargetFrameRate;
                    _depthFpsText.text =
                        string.Format(DisplayFormat, DepthLabel, _occlusionExtension.TargetFrameRate);

                    _depthFramerateSlider.onValueChanged.AddListener
                    (
                        value => OnSliderValueChange(DepthLabel, value)
                    );
                }
                else
                {
                    Debug.LogWarning("Occlusion extension is null: cannot adjust depth framerate");
                }
            }

            if (_segmentationFramerateSlider is not null)
            {
                _segmentationFramerateSlider.transform.parent.gameObject.SetActive(_segmentationManager is not null);
                if (_segmentationManager is not null)
                {
                    _segmentationFramerateSlider.value = _segmentationManager.TargetFrameRate;
                    _segmentationFpsText.text =
                        string.Format(DisplayFormat, SemanticsLabel, _segmentationManager.TargetFrameRate);

                    _segmentationFramerateSlider.onValueChanged.AddListener
                    (
                        value =>
                        OnSliderValueChange(SemanticsLabel, value)
                    );
                }
                else
                {
                    Debug.LogWarning("Segmentation manager is null: cannot adjust segmentation framerate");
                }
            }

            if (_meshingFramerateSlider is not null)
            {
                _meshingFramerateSlider.transform.parent.gameObject.SetActive(_meshingExtension is not null);
                if (_meshingExtension is not null)
                {
                    _meshingFramerateSlider.value = _meshingExtension.TargetFrameRate;
                    _meshingFpsText.text =
                        string.Format(DisplayFormat, MeshingLabel, _meshingExtension.TargetFrameRate);

                    _meshingFramerateSlider.onValueChanged.AddListener
                    (
                        value => OnSliderValueChange(MeshingLabel, value)
                    );
                }
                else
                {
                    Debug.LogWarning("Meshing extension is null: cannot adjust meshing framerate");
                }
            }

            if (_objectDetectionFramerateSlider)
            {
                _objectDetectionFramerateSlider.transform.parent.gameObject.SetActive(
                    _objectDetectionManager is not null);
                if (_objectDetectionManager is not null)
                {
                    _objectDetectionFramerateSlider.value = _objectDetectionManager.TargetFrameRate;
                    _objectDetectionFpsText.text =
                        string.Format(DisplayFormat, ObjectDetectionLabel, _objectDetectionManager.TargetFrameRate);

                    _objectDetectionFramerateSlider.onValueChanged.AddListener
                    (
                        value => OnSliderValueChange(ObjectDetectionLabel, value)
                    );
                }
                else
                {
                    Debug.LogWarning("Object detection manager is null: cannot adjust object detection framerate");
                }
            }
        }

        public void ToggleFramerateSlider()
        {
            _framerateSliderCanvasObject.SetActive(!_framerateSliderCanvasObject.activeSelf);
        }

        private void OnSliderValueChange(string sliderName, float value)
        {
            switch (sliderName)
            {
                case DepthLabel:
                    _occlusionExtension.TargetFrameRate = (uint)value;
                    _depthFpsText.text =
                        string.Format(DisplayFormat, DepthLabel, _occlusionExtension.TargetFrameRate);

                    if (_meshingExtension is not null && _isUsingLightshipDepth)
                    {
                        // Ensure that the meshing framerate is not higher than the depth framerate
                        _meshingExtension.TargetFrameRate =
                            (int)Math.Min(_meshingExtension.TargetFrameRate, _occlusionExtension.TargetFrameRate);
                        _meshingFpsText.text =
                            string.Format(DisplayFormat, MeshingLabel, _meshingExtension.TargetFrameRate);
                        _meshingFramerateSlider.value = _meshingExtension.TargetFrameRate;
                    }
                    break;

                case SemanticsLabel:
                    _segmentationManager.TargetFrameRate = (uint)value;
                    _segmentationFpsText.text =
                        string.Format(DisplayFormat, SemanticsLabel, _segmentationManager.TargetFrameRate);
                    break;

                case MeshingLabel:
                    // Ensure that the meshing framerate is not higher than the depth framerate for Lightship depth
                    _meshingExtension.TargetFrameRate =
                        _isUsingLightshipDepth
                            ? (int)Math.Min(value, _occlusionExtension.TargetFrameRate)
                            : (int)value;

                    _meshingFpsText.text =
                        string.Format(DisplayFormat, MeshingLabel, _meshingExtension.TargetFrameRate);

                    _meshingFramerateSlider.value = _meshingExtension.TargetFrameRate;
                    break;

                case ObjectDetectionLabel:
                    _objectDetectionManager.TargetFrameRate = (uint)value;
                    _objectDetectionFpsText.text =
                        string.Format(DisplayFormat, ObjectDetectionLabel, _objectDetectionManager.TargetFrameRate);
                    break;

                default:
                    Debug.LogWarning("Invalid slider name");
                    break;
            }
        }
    }
}
