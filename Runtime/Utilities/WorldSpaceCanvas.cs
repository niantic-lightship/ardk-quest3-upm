using System;
using System.Collections;
using Niantic.Lightship.AR;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.MetaQuest
{
    public class WorldSpaceCanvas : MonoBehaviour
    {
        [SerializeField]
        private ARCameraManager _arCameraManager;

        [SerializeField]
        private Canvas _canvas;

        [SerializeField]
        private float _canvasDistance = 100.0f;

        private bool _initialized;

        private IEnumerator Start()
        {
            // Wait for the first camera image
            while (!_initialized)
            {
                // Set the canvas size based on the image resolution
                if (_arCameraManager.subsystem != null &&
                    _arCameraManager.subsystem.TryGetIntrinsics(out var intrinsics))
                {
                    ScaleCameraCanvas(intrinsics);
                    _initialized = true;
                }

                yield return null;
            }
        }

        private void Update()
        {
            if (!_initialized)
            {
                return;
            }

            if (InputReader.TryGetPose(out var pose, excludeDisplayRotation: true))
            {
                var position = pose.ToPosition();
                var orientation = pose.ToRotation();

                // Position the canvas in front of the camera
                _canvas.transform.position = position + orientation * Vector3.forward * _canvasDistance;
                _canvas.transform.rotation = orientation;
            }
        }

        /// <summary>
        /// Calculate the dimensions of the canvas based on the distance from the camera origin and the camera resolution
        /// </summary>
        private void ScaleCameraCanvas(XRCameraIntrinsics intrinsics)
        {
            RectTransform cameraCanvasRectTransform = _canvas.GetComponentInChildren<RectTransform>();
            Ray leftSidePointInCamera =
                ScreenPointToRayInCamera(intrinsics, new Vector2Int(0, intrinsics.resolution.y / 2));
            Ray rightSidePointInCamera = ScreenPointToRayInCamera(intrinsics,
                new Vector2Int(intrinsics.resolution.x, intrinsics.resolution.y / 2));
            float horizontalFoVDegrees =
                Vector3.Angle(leftSidePointInCamera.direction, rightSidePointInCamera.direction);
            double horizontalFoVRadians = horizontalFoVDegrees / 180 * Math.PI;
            double newCanvasWidthInMeters = 2 * _canvasDistance * Math.Tan(horizontalFoVRadians / 2);
            float localScale = (float)(newCanvasWidthInMeters / cameraCanvasRectTransform.sizeDelta.x);
            cameraCanvasRectTransform.localScale = new Vector3(localScale, localScale, localScale);
        }

        private static Ray ScreenPointToRayInCamera(XRCameraIntrinsics intrinsics, Vector2Int screenPoint)
        {
            Vector3 directionInCamera = new Vector3
            {
                x = (screenPoint.x - intrinsics.principalPoint.x) / intrinsics.focalLength.x,
                y = (screenPoint.y - intrinsics.principalPoint.y) / intrinsics.focalLength.y,
                z = 1
            };

            return new Ray(Vector3.zero, directionInCamera);
        }
    }
}
