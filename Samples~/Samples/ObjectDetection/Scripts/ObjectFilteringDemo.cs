// Copyright 2022-2024 Niantic.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Niantic.Lightship.AR.ObjectDetection;
using Niantic.Lightship.AR.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace Niantic.Lightship.MetaQuest.InternalSamples
{
    public class ObjectFilteringDemo : MonoBehaviour
    {
        [SerializeField]
        private DrawRect _drawRect;

        [SerializeField]
        [Tooltip("Slider GameObject to set probability threshold")]
        private Slider _probabilityThresholdSlider;

        [SerializeField]
        [Tooltip("Text to display current slider value")]
        private Text _probabilityThresholdText;

        [SerializeField]
        [Tooltip("Dropdown menu to select the category to display")]
        private Dropdown _categoryDropdown;

        [SerializeField] [Tooltip("Categories to display in the dropdown")]
        private List<string> _categoryNames;

        [SerializeField]
        private Text _optionalStatusText;

        [SerializeField]
        private ARObjectDetectionManager _objectDetectionManager;

        [SerializeField]
        private float _probabilityThreshold = 0.5f;

        // The name of the actively selected semantic category
        private string _selectedCategoryName = string.Empty;
        private readonly Dictionary<string, Color> _categoriesToColors = new();

        // Colors to assign to each category
        private readonly Color[] _colors =
        {
            Color.red, Color.blue, Color.green, Color.yellow, Color.magenta, Color.cyan, Color.white, Color.black
        };

        private void Start()
        {
            // Set the probability threshold to the default value
            _probabilityThresholdSlider.value = _probabilityThreshold;
        }

        private void OnEnable()
        {
            _categoryDropdown.onValueChanged.AddListener(CategoryDropdown_OnValueChanged);
            _probabilityThresholdSlider.onValueChanged.AddListener(ProbabilityThresholdSlider_OnThresholdChanged);
            _objectDetectionManager.MetadataInitialized += ObjectDetectionManager_OnMetadataInitialized;
            _objectDetectionManager.ObjectDetectionsUpdated += ObjectDetectionManager_ObjectDetectionsUpdated;
        }

        private void OnDisable()
        {
            if (_categoryDropdown != null)
            {
                _categoryDropdown.onValueChanged.RemoveListener(CategoryDropdown_OnValueChanged);
            }

            if (_probabilityThresholdSlider != null)
            {
                _probabilityThresholdSlider.onValueChanged.RemoveListener(
                    ProbabilityThresholdSlider_OnThresholdChanged);
            }

            _objectDetectionManager.MetadataInitialized -= ObjectDetectionManager_OnMetadataInitialized;
            _objectDetectionManager.ObjectDetectionsUpdated -= ObjectDetectionManager_ObjectDetectionsUpdated;
        }

        private void ObjectDetectionManager_OnMetadataInitialized(ARObjectDetectionModelEventArgs args)
        {
            // Display All categories by default.
            _selectedCategoryName = _categoryNames[0];

            _categoryDropdown.AddOptions(_categoryNames.ToList());

            var dropdownList = _categoryDropdown.options.Select(option => option.text).ToList();
            _categoryDropdown.value = dropdownList.IndexOf(_selectedCategoryName);
        }

        private void ObjectDetectionManager_ObjectDetectionsUpdated(ARObjectDetectionsUpdatedEventArgs args)
        {
            // Clear the previous bounding boxes
            _drawRect.ClearRects();

            var result = args.Results;
            if (result == null || result.Count == 0)
            {
                return;
            }

            // Get the viewport resolution
            var viewport = _drawRect.GetComponent<RectTransform>();
            int viewportWidth = Mathf.FloorToInt(viewport.rect.width);
            int viewportHeight = Mathf.FloorToInt(viewport.rect.height);

            // Log detection info to the UI?
            var displayStatus = _optionalStatusText != null;
            if (displayStatus)
            {
                _optionalStatusText.text = "Viewport resolution: " + viewportWidth + "x" + viewportHeight + "\n";
            }

            foreach (var detection in args.Results)
            {
                // Determine the classification category of this detection
                string categoryName;
                if (_selectedCategoryName.Equals("All", StringComparison.OrdinalIgnoreCase))
                {
                    // Acquire the categorizations for this detected object
                    var categorizations = detection.GetConfidentCategorizations(_probabilityThreshold);
                    if (categorizations.Count <= 0)
                    {
                        continue;
                    }

                    // Sort the categorizations by confidence and select the most confident one
                    categoryName = categorizations.Aggregate((a, b) => a.Confidence > b.Confidence ? a : b)
                        .CategoryName;
                }
                else
                {
                    // Get name and confidence of the detected object in a given category.
                    categoryName = _selectedCategoryName;
                }

                // Filter out the objects with confidence less than the threshold
                float confidence = detection.GetConfidence(categoryName);
                if (confidence < _probabilityThreshold)
                {
                    continue;
                }

                // Get the bounding rect around the detected object
                var rect = detection.CalculateRect(viewportWidth, viewportHeight,
                    XRDisplayContext.GetScreenOrientation());

                // Draw the bounding rect around the detected object
                var info = $"{categoryName}: {confidence}\n";
                _drawRect.CreateRect(rect, GetOrAssignColorToCategory(categoryName), info);

                if (displayStatus)
                {
                    _optionalStatusText.text += info;
                }
            }
        }

        private void ProbabilityThresholdSlider_OnThresholdChanged(float newThreshold)
        {
            _probabilityThreshold = newThreshold;
            _probabilityThresholdText.text = newThreshold.ToString(CultureInfo.InvariantCulture);
        }

        private void CategoryDropdown_OnValueChanged(int val)
        {
            // Update the display category from the dropdown value.
            _selectedCategoryName = _categoryDropdown.options[val].text;
        }

        private Color GetOrAssignColorToCategory(string categoryName)
        {
            if (!_categoriesToColors.TryGetValue(categoryName, out var color))
            {
                color = _colors[UnityEngine.Random.Range(0, _colors.Length)];
                _categoriesToColors.Add(categoryName, color);
            }

            return color;
        }
    }
}
