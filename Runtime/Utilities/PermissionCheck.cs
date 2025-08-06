using UnityEngine;
using UnityEngine.Serialization;

namespace Niantic.Lightship.MetaQuest
{
#if UNITY_ANDROID
    using UnityEngine.Android;
#endif // UNITY_ANDROID
    using UnityEngine.Events;

    public class PermissionsCheck : MonoBehaviour
    {
        private const string DefaultPermissionId = "com.oculus.permission.USE_SCENE";

#pragma warning disable CS0414
        [SerializeField]
        [Tooltip("The Android system permission to request")]
        private string _permissionId = DefaultPermissionId;

        [SerializeField]
        [Tooltip("Invoked when permission is denied")]
        private UnityEvent<string> _permissionDenied;

        [SerializeField]
        [Tooltip("Invoked when permission is granted")]
        private UnityEvent<string> _permissionGranted;
#pragma warning restore CS0414

#if UNITY_ANDROID
        private void Start()
        {
            if (Permission.HasUserAuthorizedPermission(_permissionId))
            {
                OnPermissionGranted(_permissionId);
            }
            else
            {
                var callbacks = new PermissionCallbacks();
                callbacks.PermissionDenied += OnPermissionDenied;
                callbacks.PermissionGranted += OnPermissionGranted;

                Debug.Log($"Requesting permission for: {_permissionId}");
                Permission.RequestUserPermission(_permissionId, callbacks);
            }
        }

        private void OnPermissionDenied(string permission)
        {
            Debug.LogWarning($"User denied permission for: {_permissionId}");
            _permissionDenied.Invoke(permission);
        }

        private void OnPermissionGranted(string permission)
        {
            Debug.Log($"User granted permission for: {_permissionId}");
            _permissionGranted.Invoke(permission);
        }
#endif // UNITY_ANDROID
    }
}
