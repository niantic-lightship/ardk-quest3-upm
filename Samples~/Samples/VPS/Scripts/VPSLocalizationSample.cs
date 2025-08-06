// Copyright 2022-2025 Niantic.

using System.Collections.Generic;
using System.Linq;
using Niantic.Lightship.AR;
using Niantic.Lightship.AR.Loader;
using Niantic.Lightship.AR.LocationAR;
using Niantic.Lightship.AR.PersistentAnchors;
using Niantic.Lightship.AR.XRSubsystems;
using UnityEngine;
using UnityEngine.UI;

using Input = Niantic.Lightship.AR.Input;

namespace Niantic.Lightship.MetaQuest.InternalSamples
{
    public class VPSLocalizationSample : MonoBehaviour
    {
        [SerializeField, Tooltip("The Location Manager")]
        private ARLocationManager _arLocationManager;

        [Header("UI")]
        [SerializeField, Tooltip("The Dropdown for Persistent AR Locations")]
        private Dropdown _arLocationDropdown;

        [SerializeField, Tooltip("The Button to select an AR Location")]
        private Button _localizeButton;

        [SerializeField, Tooltip("The UI Canvas to display the AR Location Selector")]
        private GameObject _arLocationUI;

        [SerializeField]
        private Text _localizationStatusDisplayText;

        [SerializeField]
        private Text _gpsDisplayText;

        [SerializeField]
        private Text _debugDisplayText;

        private readonly List<ARLocation> _arLocationsDropdownItems = new();
        private bool _localizationStatus;

        private void OnEnable()
        {
            _localizeButton.onClick.AddListener(LocalizeButton_OnClick);
            _arLocationManager.locationTrackingStateChanged += OnLocationTrackingStateChanged;
        }

        private void OnDisable()
        {
            _localizeButton.onClick.RemoveListener(LocalizeButton_OnClick);
            _arLocationManager.locationTrackingStateChanged -= OnLocationTrackingStateChanged;
        }

        private void Start()
        {
            // Verify settings
            if (LightshipSettingsHelper.ActiveSettings.LocationAndCompassDataSource != LocationDataSource.Spoof)
            {
                Debug.LogError
                (
                    "Meta Quest does not provide GPS, which is necessary for Lightship VPS. " +
                    "Please enable the Spoof Location feature in the Lightship Settings menu in " +
                    "order to try out the VPS Localization Sample."
                );
                _debugDisplayText.text =
                    "Error: Spoof Location is not enabled. The Meta Quest does not provide GPS which Lightship VPS requires. Please enable Location Spoofing in Lightship Settings.";

                Destroy(this);
                return;
            }

            // Initialize the UI elements
            _debugDisplayText.text = string.Empty;
            _arLocationUI.SetActive(!_arLocationManager.AutoTrack);
            _localizeButton.GetComponentInChildren<Text>().text =
                _arLocationManager.AutoTrack ? "Automatic Localization" : "Start Localization";

            if (!_arLocationManager.AutoTrack)
            {
                CreateARLocationMenu();
            }

            // Start the location service
            if (Input.location.status == LocationServiceStatus.Stopped)
            {
                UpdateStatusText("Stopped");
                Input.location.Start();
            }

            UpdateGpsText();

            if (_arLocationManager.subsystem != null)
            {
                _arLocationManager.subsystem.debugInfoProvided += OnDebugInfoProvided;
            }
            else
            {
                Debug.LogError("No XRPersistentAnchorSubsystem found.");
                _debugDisplayText.text =
                    "Error: No XRPersistentAnchorSubsystem found.";
            }
        }

        private void OnDestroy()
        {
            if (_arLocationManager.subsystem != null)
            {
                _arLocationManager.subsystem.debugInfoProvided -= OnDebugInfoProvided;
            }
        }

        private void CreateARLocationMenu()
        {
            var arLocations = _arLocationManager.ARLocations;
            foreach (var arLocation in arLocations)
            {
                _arLocationDropdown.options.Add(new Dropdown.OptionData(arLocation.name));
                _arLocationsDropdownItems.Add(arLocation);
            }

            if (_arLocationsDropdownItems.Count > 0)
            {
                _localizeButton.interactable = true;
            }
        }

        private void UpdateGpsText()
        {
            // First, check if user has location service enabled
            if (!Input.location.isEnabledByUser)
            {
                _gpsDisplayText.text = "Location service is not enabled";
                return;
            }

            var latitude = Input.location.lastData.latitude;
            var longitude = Input.location.lastData.longitude;
            var altitude = Input.location.lastData.altitude;

            _gpsDisplayText.text = $"Latitude: {latitude}\nLongitude: {longitude}\nAltitude: {altitude}";
        }

        private void UpdateStatusText(string status)
        {
            _localizationStatusDisplayText.text = "Status: " + status;
        }

        private void LocalizeButton_OnClick()
        {
            if (_arLocationManager.AutoTrack)
            {
                return;
            }

            UpdateGpsText();
            UpdateStatusText(!_localizationStatus ? "Trying to localize..." : "Stopped");
            _localizeButton.GetComponentInChildren<Text>().text =
                !_localizationStatus ? "Stop Localization" : "Start Localization";

            if (!_localizationStatus)
            {
                var currentIndex = _arLocationDropdown.value;
                var arLocation = _arLocationsDropdownItems[currentIndex];
                _arLocationManager.SetARLocations(arLocation);
                _arLocationManager.StartTracking();
                _localizationStatus = true;
            }
            else
            {
                _arLocationManager.StopTracking();
                _localizationStatus = false;
            }
        }

        private void OnLocationTrackingStateChanged(ARLocationTrackedEventArgs args)
        {
            args.ARLocation.gameObject.SetActive(args.Tracking);
            UpdateStatusText(
                $"{(args.Tracking ? "Tracking Success" : $"Tracking Failure (reason: {args.TrackingStateReason})")}");
        }

        private void OnDebugInfoProvided(XRPersistentAnchorDebugInfo info)
        {
            var errors = info.networkStatusArray.Where(s => s.Status == RequestStatus.Failed).Select(s => s.Error)
                .ToArray();
            if (errors.Length > 0)
            {
                _debugDisplayText.text = "Network errors: " + string.Join(",", errors) +
                    "\nPlease ensure you are connected to WIFI and have set a valid API key in Lightship Settings.";
            }
        }
    }
}
