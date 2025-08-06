using UnityEngine;

namespace Niantic.Lightship.MetaQuest.InternalSamples
{
    public class UIPositionUpdater : MonoBehaviour
    {
        [SerializeField]
        private Transform _cameraTransform;

        [SerializeField]
        private float _distanceFromCamera = 1.0f;

        [SerializeField]
        private Vector3 _positionOffset = Vector3.zero;

        [SerializeField]
        private bool _lockZRotation = true;

        [SerializeField]
        private float _positionLerpSpeed = 4.0f;

        [SerializeField]
        private float _rotationLerpSpeed = 4.0f;

        private void Start()
        {
            if (_cameraTransform == null)
            {
                if (Camera.main != null)
                {
                    _cameraTransform = Camera.main.transform;
                }
            }

            if (_cameraTransform == null)
            {
                Debug.LogError("No camera transform found. Please assign a camera transform.");
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            // Target position in front of the camera
            Vector3 targetPosition = _cameraTransform.position + _cameraTransform.forward * _distanceFromCamera + _positionOffset;

            // Smoothly interpolate position
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * _positionLerpSpeed);

            // Target rotation
            Quaternion targetRotation = _lockZRotation
                ? Quaternion.LookRotation(transform.position - _cameraTransform.position, _cameraTransform.up)
                : Quaternion.LookRotation(transform.position - _cameraTransform.position);

            // Smoothly interpolate rotation
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * _rotationLerpSpeed);
        }
    }
}
