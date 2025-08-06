// Copyright 2022-2025 Niantic.

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.MetaQuest
{
    /// <summary>
    /// Caches the intrinsics from an occlusion frame.
    /// <remarks>
    /// This was created because as of today, it is not possible to extract the intrinsics directly
    /// from the xr occlusion subsystem. Calling OcclusionSubsystem.TryGetFrame() will yield
    /// a duplicate call error from OpenXR:
    ///     xrAcquireEnvironmentDepthImageMETAImpl: Already acquired a depth image in this render iteration
    /// This is because the occlusion manager is already calling subsystem.TryGetFrame() internally every frame.
    /// </remarks>
    /// </summary>
    internal static class OcclusionIntrinsicsBootstrap
    {
        private static XRCameraIntrinsics? s_intrinsics;
        private static AROcclusionManager s_occlusionManagerInstance;

        /// <summary>
        /// Whether the intrinsics for the depth image have been set.
        /// </summary>
        public static bool HasIntrinsics => s_intrinsics.HasValue;

        /// <summary>
        /// The intrinsics for the depth image.
        /// </summary>
        public static XRCameraIntrinsics Intrinsics => s_intrinsics ?? default(XRCameraIntrinsics);

        /// <summary>
        /// This is called after the very first scene is loaded (after Awake).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AfterSceneLoad()
        {
            // Look for an occlusion manager and try to subscribe to its frame updates
            Subscribe();

            // Do the same on subsequent scene changes
            SceneManager.sceneLoaded += SceneManager_OnSceneLoaded;
        }

        /// <summary>
        /// This is called on every scene load (after OnEnable).
        /// </summary>
        private static void SceneManager_OnSceneLoaded(Scene arg0, LoadSceneMode arg1)
        {
            // Look for an occlusion manager and try to subscribe to its frame updates
            Subscribe();
        }

        private static void Subscribe()
        {
            // If not subscribed and doesn't have intrinsics yet
            if (!s_occlusionManagerInstance && !HasIntrinsics)
            {
                // Try to find the manager in the scene
                s_occlusionManagerInstance = Object.FindFirstObjectByType<AROcclusionManager>();
                if (s_occlusionManagerInstance)
                {
                    // Subscribe to the frame received event
                    s_occlusionManagerInstance.frameReceived += AROcclusionManager_OnFrameReceived;
                }
            }
        }

        private static void Unsubscribe()
        {
            if (s_occlusionManagerInstance)
            {
                s_occlusionManagerInstance.frameReceived -= AROcclusionManager_OnFrameReceived;
            }
            s_occlusionManagerInstance = null;
        }

        private static void AROcclusionManager_OnFrameReceived(AROcclusionFrameEventArgs args)
        {
            // Grab the intrinsics from the occlusion frame
            if (GrabIntrinsics(args))
            {
                Unsubscribe();
            }
        }

        private static bool GrabIntrinsics(AROcclusionFrameEventArgs data)
        {
            if (!data.TryGetFovs(out var fovArray))
            {
                return false;
            }

            if (data.externalTextures == null || data.externalTextures.Count == 0)
            {
                return false;
            }

            var texture = data.externalTextures[0].texture;
            if (!texture)
            {
                return false;
            }

            // Get the left eye FOV
            var leftEyeFov = fovArray[0];

            // Convert to tangents
            var tanLeft = Mathf.Tan(leftEyeFov.angleLeft);
            var tanRight = Mathf.Tan(leftEyeFov.angleRight);
            var tanUp = Mathf.Tan(leftEyeFov.angleUp);
            var tanDown = Mathf.Tan(leftEyeFov.angleDown);

            // Calculate the full focal lengths
            float fovX = Mathf.Abs(tanLeft) + Mathf.Abs(tanRight);
            float fovY = Mathf.Abs(tanUp) + Mathf.Abs(tanDown);

            var width = texture ? texture.width : 320;
            var height = texture ? texture.height : 320;

            s_intrinsics = new XRCameraIntrinsics(
                focalLength: new Vector2(width / fovX, height / fovY),
                principalPoint: new Vector2(width * Mathf.Abs(tanLeft) / fovX, height * Mathf.Abs(tanUp) / fovY),
                resolution: new Vector2Int(width, height));

            return true;
        }
    }
}
