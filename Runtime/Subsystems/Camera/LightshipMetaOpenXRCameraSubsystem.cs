using System;
using System.Runtime.InteropServices;
using Niantic.Lightship.AR;
using Niantic.Lightship.AR.Subsystems.Camera;
using Niantic.Lightship.AR.Utilities;
using Niantic.Lightship.MetaQuest.Runtime.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Meta;

namespace Niantic.Lightship.MetaQuest
{
    /// <summary>
    /// A substitute implementation to <see cref="MetaOpenXRCameraSubsystem"/>.
    /// In addition to the original functionality, this class also provides
    /// access to the camera image. Requires OpenXR.
    /// </summary>
    public sealed class LightshipMetaOpenXRCameraSubsystem : XRCameraSubsystem
    {
        /// <summary>
        /// The identifier for the subsystem.
        /// </summary>
        internal const string SubsystemId = "Lightship-Meta-Camera";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterDescriptor()
        {
            var cameraSubsystemCinfo = new XRCameraSubsystemDescriptor.Cinfo
            {
                id = SubsystemId,
                providerType = typeof(LightshipMetaOpenXRProvider),
                subsystemTypeOverride = typeof(LightshipMetaOpenXRCameraSubsystem),
                supportsAverageBrightness = false,
                supportsAverageColorTemperature = false,
                supportsColorCorrection = false,
                supportsProjectionMatrix = false,
                supportsCameraConfigurations = false,
                supportsAverageIntensityInLumens = false,
                supportsFocusModes = false,
                supportsFaceTrackingAmbientIntensityLightEstimation = false,
                supportsFaceTrackingHDRLightEstimation = false,
                supportsWorldTrackingAmbientIntensityLightEstimation = false,
                supportsWorldTrackingHDRLightEstimation = false,
                supportsCameraGrain = false,

                // Overrides relative to MetaOpenXRCameraSubsystem
                supportsCameraImage = true,
                supportsDisplayMatrix = true,
                supportsTimestamp = true,
            };

            XRCameraSubsystemDescriptor.Register(cameraSubsystemCinfo);
        }

        /// <summary>
        /// Returns the camera world pose associated with the last acquired image.
        /// </summary>
        /// <remarks>
        /// This is the world pose of the recording camera on the headset.
        /// This matrix is often referred to as the extrinsics matrix.
        /// </remarks>
        public bool TryGetCameraPose(out Matrix4x4 extrinsics)
        {
            if (provider is LightshipMetaOpenXRProvider lsProvider)
            {
                return lsProvider.TryGetCameraPose(out extrinsics);
            }

            extrinsics = default;
            return false;
        }

        /// <summary>
        /// Returns the pose of the device.
        /// </summary>
        /// <remarks>This is the head pose (world) on headsets.</remarks>
        public bool TryGetHeadPose(out Matrix4x4 pose)
        {
            if (provider is LightshipMetaOpenXRProvider lsProvider)
            {
                pose = lsProvider.GetHeadPose();
                return true;
            }

            pose = default;
            return false;
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            LightshipInputDevice.AddDevice<LightshipMetaOpenXRCameraDevice>(LightshipMetaOpenXRCameraDevice.ProductName);
        }

        protected override void OnDestroy()
        {
            LightshipInputDevice.RemoveDevice<LightshipMetaOpenXRCameraDevice>();
            base.OnDestroy();
        }

        protected override void OnStart()
        {
            base.OnStart();
            InputSystem.onBeforeUpdate += InputSystem_OnBeforeUpdate;
        }

        protected override void OnStop()
        {
            InputSystem.onBeforeUpdate -= InputSystem_OnBeforeUpdate;
            base.OnStop();
        }

        /// <summary>
        /// Callback for when it is time to query the current camera pose.
        /// </summary>
        private void InputSystem_OnBeforeUpdate()
        {
            var device = InputSystem.GetDevice<LightshipMetaOpenXRCameraDevice>();
            if (device == null)
            {
                return;
            }

            if (provider is LightshipMetaOpenXRProvider lsProvider)
            {
                // Get the current time in milliseconds
                double now = OVRPlugin.GetTimeInSeconds() * 1000.0;

                // Pull the most recent poses
                var headPose = LightshipMetaOpenXRProvider.GetHeadPose(now);
                var cameraPose = lsProvider.TryGetCameraPose(now, out Matrix4x4 pose)
                    ? pose
                    : Matrix4x4.identity;

                // Propagate the update through the input device
                device.PushUpdate(
                    headPose.ToPosition(), headPose.ToRotation(),
                    cameraPose.ToPosition(), cameraPose.ToRotation(),
                    DeviceOrientation.LandscapeLeft, timestampMs: now);
            }
        }

        /// <summary>
        /// Renders the camera background using the Meta OpenXR passthrough API.
        /// Provides the camera image by deriving the Lightship XR Camera Subsystem Provider.
        /// </summary>
        private sealed class LightshipMetaOpenXRProvider : LightshipXRCameraSubsystemProvider
        {
            /// <summary>
            /// Is the OpenXR Meta passthrough API enabled?
            /// </summary>
            private bool _isOpenXRPassthroughEnabled;

            /// <summary>
            /// Matrix that transforms from head pose to the recording camera pose.
            /// </summary>
            private Matrix4x4? _headToCameraMatrix;

            public LightshipMetaOpenXRProvider()
                // Support texture descriptors to allow the camera subsystem to
                // trigger frame updates through the ARCameraManager, pulling
                // the latest camera image every frame.
                : base(supportsTextureDescriptors: true)
            {
            }

            /// <inheritdoc />
            protected override bool TryInitialize()
            {
                // Evaluate whether the OpenXR passthrough extension is enabled
                _isOpenXRPassthroughEnabled =
                    OpenXRRuntime.IsExtensionEnabled(LightshipARCameraFeature.KOpenXRMetaPassthroughExtension);

                if (_isOpenXRPassthroughEnabled)
                {
                    // Initialize the passthrough API (for rendering the camera background)
                    MetaOpenXRNativeApi.UnityMetaQuest_Passthrough_Construct();
                }

                // Initialize the native camera image provider
                return base.TryInitialize();
            }

            /// <summary>
            /// Start the camera functionality.
            /// </summary>
            public override void Start()
            {
                if (_isOpenXRPassthroughEnabled)
                {
                    // Start the background rendering
                    MetaOpenXRNativeApi.UnityMetaQuest_Passthrough_Start();
                }

                // Start the native camera image provider
                base.Start();
            }

            /// <summary>
            /// Stop the camera functionality.
            /// </summary>
            public override void Stop()
            {
                if (_isOpenXRPassthroughEnabled)
                {
                    // Stop the background rendering
                    MetaOpenXRNativeApi.UnityMetaQuest_Passthrough_Stop();
                }

                // Stop the native camera image provider
                base.Stop();
            }

            /// <summary>
            /// Destroy any resources required for the camera functionality.
            /// </summary>
            public override void Destroy()
            {
                if (_isOpenXRPassthroughEnabled)
                {
                    // Destroy the passthrough API resources (background renderer)
                    MetaOpenXRNativeApi.UnityMetaQuest_Passthrough_Destruct();
                }

                // Destroy the native camera image provider
                base.Destroy();
            }

            /// <summary>
            /// Get the camera frame for the subsystem.
            /// </summary>
            /// <param name="cameraParams">The current Unity <c>Camera</c> parameters.</param>
            /// <param name="cameraFrame">The current camera frame returned by the method.</param>
            /// <returns><see langword="true"/> if the method successfully got a frame. Otherwise, <see langword="false"/>.</returns>
            public override bool TryGetFrame(XRCameraParams cameraParams, out XRCameraFrame cameraFrame)
            {
                const XRCameraFrameProperties props = XRCameraFrameProperties.Timestamp |
                    XRCameraFrameProperties.DisplayMatrix;

                var timestampMs = LastImageTimestampMs;
                var displayMatrix = AffineMath.s_invertVertical;

                cameraFrame = new XRCameraFrame
                (
                    (long)(timestampMs * 1000000), // seconds * 1e+6
                    0,
                    0,
                    default,
                    default,
                    displayMatrix,
                    TrackingState.Tracking,
                    IntPtr.Zero,
                    props,
                    0,
                    0,
                    0,
                    0,
                    default,
                    Vector3.forward,
                    default,
                    default,
                    0
                );

                return true;
            }

            /// <summary>
            /// Get the camera intrinsics information.
            /// </summary>
            /// <param name="cameraIntrinsics">The camera intrinsics information returned from the method.</param>
            /// <returns><see langword="true"/> if the method successfully gets the camera intrinsics information.
            /// Otherwise, <see langword="false"/>.</returns>
            public override bool TryGetIntrinsics(out XRCameraIntrinsics cameraIntrinsics)
            {
                if (base.TryGetIntrinsics(out var intrinsics))
                {
                    if (CameraSupport.IsEarlyVersion)
                    {
                        cameraIntrinsics = new XRCameraIntrinsics(
                            // The early API release contains a bug resulting in focal lengths
                            // being returned at half their actual value
                            focalLength: intrinsics.focalLength * 2.0f,
                            principalPoint: intrinsics.principalPoint,
                            resolution: intrinsics.resolution);
                    }
                    else
                    {
                        cameraIntrinsics = intrinsics;
                    }

                    return true;
                }

                cameraIntrinsics = default;
                return false;
            }

            /// <summary>
            /// Returns the camera pose at the time when the last acquired image was captured.
            /// </summary>
            /// <remarks>
            /// This is the pose of the recording camera and not the pose of the headset.
            /// This matrix is often referred to as camera extrinsics.
            /// </remarks>
            /// <param name="extrinsics">The world pose of the camera used to capture the latest image.</param>
            /// <returns>True, if the camera pose was successfully retrieved; otherwise, false.</returns>
            public bool TryGetCameraPose(out Matrix4x4 extrinsics) =>
                TryGetCameraPoseFromHeadPose(GetHeadPose(LastImageTimestampMs), out extrinsics);

            /// <summary>
            /// Returns the camera pose at the specified timestamp.
            /// </summary>
            /// <remarks>
            /// This is the pose of the recording camera and not the pose of the headset.
            /// This matrix is often referred to as camera extrinsics.
            /// </remarks>
            /// <param name="timestampMs">The timestamp in milliseconds.</param>
            /// <param name="extrinsics">The world pose of the camera used to capture the latest image.</param>
            /// <returns>True, if the camera pose was successfully retrieved; otherwise, false.</returns>
            public bool TryGetCameraPose(double timestampMs, out Matrix4x4 extrinsics) =>
                TryGetCameraPoseFromHeadPose(GetHeadPose(timestampMs), out extrinsics);

            /// <summary>
            /// Returns the pose of the headset at the time when the last acquired image was captured.
            /// </summary>
            /// <remarks>This is the pose of the headset and not the camera.</remarks>
            /// <returns>The world pose of the headset.</returns>
            public Matrix4x4 GetHeadPose() => GetHeadPose(LastImageTimestampMs);

            /// <summary>
            /// Returns the pose of the headset at the specified timestamp.
            /// </summary>
            /// <param name="timestampMs">Timestamp in milliseconds.</param>
            /// <returns>The world pose of the headset.</returns>
            public static Matrix4x4 GetHeadPose(double timestampMs)
            {
                var timeInSeconds = timestampMs / 1000.0;
                var devicePose = OVRPlugin.GetNodePoseStateAtTime(timeInSeconds, OVRPlugin.Node.Head).Pose.ToOVRPose();
                return Matrix4x4.TRS(devicePose.position, devicePose.orientation, Vector3.one);
            }

            /// <summary>
            /// Transforms a per-frame head pose into the per-frame camera pose.
            /// </summary>
            /// <param name="headPose">`Matrix4x4` returned by <c>GetHeadPose</c>.</param>
            /// <param name="cameraPose">World pose of the recording camera.</param>
            /// <returns><c>true</c> if successful.</returns>
            private bool TryGetCameraPoseFromHeadPose(Matrix4x4 headPose, out Matrix4x4 cameraPose)
            {
                if (!_headToCameraMatrix.HasValue)
                {
                    if (TryBuildHeadToCameraMatrix(out var matrix))
                    {
                        _headToCameraMatrix = matrix;
                    }
                    else
                    {
                        cameraPose = default;
                        return false;
                    }
                }

                // worldFromCamera = worldFromHead * headFromCamera
                cameraPose = headPose * _headToCameraMatrix.Value;
                return true;
            }

            /// <summary>
            /// Builds a matrix that represents the offset between the recording camera
            /// and the tracking point of the headset.
            /// </summary>
            /// <param name="headToCamera">The matrix that transforms from headset pose to camera pose.</param>
            /// <returns><c>true</c> if successful.</returns>
            private bool TryBuildHeadToCameraMatrix(out Matrix4x4 headToCamera)
            {
                if (!TryGetLensOffset(out var lensT, out var lensQ))
                {
                    headToCamera = default;
                    return false;
                }

                // Android (RH) to Unity (LH) conversions
                var t = new Vector3(lensT[0], lensT[1], -lensT[2]);
                var qCameraFromHead = new Quaternion(-lensQ[0], -lensQ[1],  lensQ[2], lensQ[3]);
                var qHeadFromCamera = Quaternion.Inverse(qCameraFromHead);

                // Matrix from camera-space to head-space ...
                headToCamera = Matrix4x4.TRS(t, qHeadFromCamera, Vector3.one);

                // ... flip the camera so its +Z looks forward and +Y up
                headToCamera *= Matrix4x4.Rotate(Quaternion.Euler(180f, 0f, 0f));

                return true;
            }

            /// <summary>
            /// Container to wrap the native Meta OpenXR passthrough APIs.
            /// <remarks>This is the original OpenXR API for rendering the passthrough background.</remarks>
            /// </summary>
            private static class MetaOpenXRNativeApi
            {
                // TODO(ahegedus): This is an external library and it needs to be guarded
                // against future changes. Either implement native passthrough rendering
                // on our own or get the vendor to expose their provider or native api.
                private const string k_ARFoundationLibrary = "libUnityARFoundationMeta";

                [DllImport(k_ARFoundationLibrary)]
                public static extern void UnityMetaQuest_Passthrough_Construct();

                [DllImport(k_ARFoundationLibrary)]
                public static extern void UnityMetaQuest_Passthrough_Destruct();

                [DllImport(k_ARFoundationLibrary)]
                public static extern void UnityMetaQuest_Passthrough_Start();

                [DllImport(k_ARFoundationLibrary)]
                public static extern void UnityMetaQuest_Passthrough_Stop();
            }
        }
    }
}
