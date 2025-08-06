using UnityEngine;
using UnityEngine.UI;

namespace Niantic.Lightship.MetaQuest.InternalSamples
{
    public class OcclusionSample : MonoBehaviour
    {
        [SerializeField]
        private OcclusionMesh _occlusionMesh;

        [SerializeField]
        private Button _visualizeButton;

        [SerializeField]
        private Text _visualizeButtonText;

        private void OnEnable()
        {
            _visualizeButton.onClick.AddListener(ToggleVisualize);
        }

        private void OnDisable()
        {
            _visualizeButton.onClick.RemoveListener(ToggleVisualize);
        }

        private void Start()
        {
            _visualizeButtonText.text = _occlusionMesh.DebugVisualization ? "Hide Depth" : "Show Depth";
        }

        private void ToggleVisualize()
        {
            _occlusionMesh.DebugVisualization = !_occlusionMesh.DebugVisualization;
            _visualizeButtonText.text = _occlusionMesh.DebugVisualization ? "Hide Depth" : "Show Depth";
        }
    }
}
