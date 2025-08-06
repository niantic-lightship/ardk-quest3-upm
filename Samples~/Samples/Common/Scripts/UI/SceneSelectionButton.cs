// Copyright 2022-2025 Niantic.

using System;
using UnityEngine;
using UnityEngine.UI;

namespace Niantic.Lightship.MetaQuest.InternalSamples
{
    [RequireComponent(typeof(Button))]
    public class SceneSelectionButton : MonoBehaviour
    {
        [SerializeField]
        private Text _buttonText;

        private Button _button;
        private string _sceneName = String.Empty;

        private void Awake()
        {
            _button = GetComponent<Button>();
        }

        private void OnEnable()
        {
            _button.onClick.AddListener(OnClick);
        }

        private void OnDisable()
        {
            _button.onClick.RemoveListener(OnClick);
        }

        private void OnClick()
        {
            if (!string.IsNullOrEmpty(_sceneName))
            {
                // Load the scene
                UnityEngine.SceneManagement.SceneManager.LoadScene(_sceneName);
            }
        }

        /// <summary>
        /// Initialize the button. Sets the correct scene to load on click.
        /// </summary>
        /// <param name="sceneName"></param>
        public void Initialize(string sceneName)
        {
            _sceneName = sceneName;
            _buttonText.text = sceneName;
        }
    }
}
