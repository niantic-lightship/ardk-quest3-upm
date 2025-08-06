// Copyright 2022-2025 Niantic.

using System;
using UnityEngine;
using UnityEngine.UI;

namespace Niantic.Lightship.MetaQuest.InternalSamples
{
    public class UIController : MonoBehaviour
    {
        [SerializeField]
        private string _title = "Title";

        [SerializeField]
        private string _description = "Enter the description here";

        [SerializeField]
        private Text _titleText;

        [SerializeField]
        private Text _descriptionText;

        [SerializeField]
        private GameObject _panelsParent;

        [SerializeField]
        private GameObject _overviewPanel;

        [SerializeField]
        private GameObject _settingsPanel;

        [SerializeField]
        private GameObject _scenesPanel;

        [SerializeField]
        private Button _overviewButton;

        [SerializeField]
        private Button _settingsButton;

        [SerializeField]
        private Button _scenesButton;

        [SerializeField]
        private Button _collapseButton;

        [SerializeField]
        private Button _exitButton;

        [SerializeField]
        private SceneSelectionButton _sceneSelectionButtonPrefab;

        [SerializeField]
        private Transform _sceneSelectionButtonParent;

        private void OnEnable()
        {
            _overviewButton.onClick.AddListener(OnOverviewButtonClicked);
            _settingsButton.onClick.AddListener(OnSettingsButtonClicked);
            _scenesButton.onClick.AddListener(OnScenesButtonClicked);
            _exitButton.onClick.AddListener(OnExitButtonClicked);
            _collapseButton.onClick.AddListener(OnCollapseButtonClicked);
        }

        private void OnDisable()
        {
            _overviewButton.onClick.RemoveListener(OnOverviewButtonClicked);
            _settingsButton.onClick.RemoveListener(OnSettingsButtonClicked);
            _scenesButton.onClick.RemoveListener(OnScenesButtonClicked);
            _exitButton.onClick.RemoveListener(OnExitButtonClicked);
            _collapseButton.onClick.RemoveListener(OnCollapseButtonClicked);
        }

        private void Awake()
        {
            // Set the title text
            if (_titleText != null)
            {
                _titleText.text = _title;
            }

            // Set the description text
            if (_descriptionText != null)
            {
                _descriptionText.text = _description;
            }

            // Check if the button prefab and parent are assigned
            if (_sceneSelectionButtonPrefab == null)
            {
                Debug.LogError("Scene Selection Button Prefab is not assigned.");
                enabled = false;
                return;
            }

            if (_sceneSelectionButtonParent == null)
            {
                Debug.LogError("Scene Selection Button Parent is not assigned.");
                enabled = false;
                return;
            }

            // Populate the scene selection buttons
            PopulateSceneButtons();
        }

        private void Start()
        {
            OnOverviewButtonClicked();
        }

        private void PopulateSceneButtons()
        {
            // Get all scenes in the build settings
            var scenes = UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings;

            for (int i = 0; i < scenes; i++)
            {
                // Get the scene name
                var sceneName = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i);
                sceneName = System.IO.Path.GetFileNameWithoutExtension(sceneName);

                // Create a new button
                var button = Instantiate(_sceneSelectionButtonPrefab, _sceneSelectionButtonParent);
                button.Initialize(sceneName);
            }
        }

        private void OnOverviewButtonClicked()
        {
            _overviewPanel.SetActive(true);
            _settingsPanel.SetActive(false);
            _scenesPanel.SetActive(false);
        }

        private void OnSettingsButtonClicked()
        {
            _overviewPanel.SetActive(false);
            _settingsPanel.SetActive(true);
            _scenesPanel.SetActive(false);
        }

        private void OnScenesButtonClicked()
        {
            _overviewPanel.SetActive(false);
            _settingsPanel.SetActive(false);
            _scenesPanel.SetActive(true);
        }

        private void OnExitButtonClicked()
        {
            // Exit the application
            Application.Quit();
        }

        private void OnCollapseButtonClicked()
        {
            // Collapse the UI
            _panelsParent.SetActive(!_panelsParent.activeSelf);
            _collapseButton.gameObject.transform.localRotation =
                Quaternion.AngleAxis(_panelsParent.activeSelf ? 90 : -90, Vector3.forward);
        }
    }
}
