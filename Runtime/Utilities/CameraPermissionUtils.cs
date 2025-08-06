using System;
using Niantic.Lightship.AR.Utilities.Logging;
using UnityEngine.Android;

namespace Niantic.Lightship.MetaQuest.Runtime.Utilities
{
    public class CameraPermissionUtils
    {
        // Required to access the Passthrough Camera API in Horizon OS v74 and above.
        public const string HorizonOSCameraPermission = "horizonos.permission.HEADSET_CAMERA";
        public const string AndroidCameraPermission = Permission.Camera;

        /// <summary>
        /// Check if both camera permissions are granted by the user.
        /// </summary>
        /// <returns>Return True if both camera permissions are granted</returns>
        public static bool HasAllPermissionsGranted()
        {
            // HORIZONOS_CAMERA_PERMISSION permission is required for v74 and above.
            if (CameraSupport.IsEarlyVersion)
            {
                return Permission.HasUserAuthorizedPermission(Permission.Camera);
            }

            return Permission.HasUserAuthorizedPermission(HorizonOSCameraPermission)
                && Permission.HasUserAuthorizedPermission(Permission.Camera);
        }

        /// <summary>
        /// Request camera permission if the permission is not authorized by the user.
        /// </summary>
        public static void AskCameraPermission(Action<bool> onComplete)
        {
            if (HasAllPermissionsGranted())
            {
                Log.Info($"PCA: All camera permissions granted.");
                onComplete?.Invoke(true);
                return;
            }

            Log.Info($"PCA: Requesting camera permissions.");
            if (CameraSupport.IsEarlyVersion)
            {
                var callbacks = new PermissionCallbacks();
                callbacks.PermissionDenied += _ => onComplete?.Invoke(false);
                callbacks.PermissionGranted += _ => onComplete?.Invoke(true);

                // For the early version of the Passthrough Camera API request only the android.permission.CAMERA permission.
                Permission.RequestUserPermission(Permission.Camera, callbacks);
            }
            else
            {
                // For OS v74 and above request both permissions.
                var permissions = new[]
                {
                    HorizonOSCameraPermission,
                    Permission.Camera
                };

                // Track the number of permissions granted and denied.
                var permissionGranted = 0;
                var callbacksReceived = 0;
                var callbacks = new PermissionCallbacks();

                // Callbacks for the permission denied event
                callbacks.PermissionDenied += _ =>
                {
                    callbacksReceived++;
                    if (callbacksReceived >= permissions.Length)
                    {
                        onComplete?.Invoke(false);
                    }
                };

                // Callbacks for the permission granted event
                callbacks.PermissionGranted += _ =>
                {
                    callbacksReceived++;
                    permissionGranted++;

                    if (callbacksReceived >= permissions.Length)
                    {
                        onComplete?.Invoke(permissionGranted >= permissions.Length);
                    }
                };

                // Request the user permissions for both camera permissions.
                Permission.RequestUserPermissions(permissions, callbacks);
            }
        }
    }
}
