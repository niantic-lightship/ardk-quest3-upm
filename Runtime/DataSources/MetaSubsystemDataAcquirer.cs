using System;
using Niantic.Lightship.AR.Core;
using Niantic.Lightship.AR.PAM;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;

namespace Niantic.Lightship.MetaQuest
{
    internal class MetaSubsystemDataAcquirer : SubsystemsDataAcquirer
    {
        // Typed camera subsystem for Meta devices using Lightship and OpenXR
        private LightshipMetaOpenXRCameraSubsystem _cameraSubsystem;

        // The environment depth texture
        private Texture2D _environmentDepthTexture2D; // internal
        private XRCpuImage _environmentDepthCpuImage; // cpu

        /// <summary>
        /// Indicates whether all required subsystems have been loaded.
        /// </summary>
        protected override bool DidLoadSubsystems => _cameraSubsystem != null;

        /// <summary>
        /// Invoked when it is time to cache the subsystem references from the XRLoader.
        /// </summary>
        protected override void OnSubsystemsLoaded(XRLoader loader)
        {
            base.OnSubsystemsLoaded(loader);
            _cameraSubsystem = loader.GetLoadedSubsystem<XRCameraSubsystem>() as LightshipMetaOpenXRCameraSubsystem;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterPamCreator()
        {
            LightshipUnityContext.CreatePamWithPlugin += LightshipUnityContext_OnCreatePam;
        }

        public override void Dispose()
        {
            LightshipUnityContext.CreatePamWithPlugin -= LightshipUnityContext_OnCreatePam;

            // Release resources
            if (_environmentDepthTexture2D != null)
            {
                UnityEngine.Object.Destroy(_environmentDepthTexture2D);
                _environmentDepthTexture2D = null;
            }

            if (_environmentDepthCpuImage.valid)
            {
                _environmentDepthCpuImage.Dispose();
            }

            XROcclusionSubsystemExtensions.ReleaseResources();
            base.Dispose();
        }

        private static PlatformAdapterManager LightshipUnityContext_OnCreatePam(
            IntPtr contextHandle,
            bool isLidarDepthEnabled,
            bool trySendOnUpdate)
        {
            return PlatformAdapterManager.Create<NativeApi, MetaSubsystemDataAcquirer>(
                contextHandle,
                isLidarDepthEnabled,
                trySendOnUpdate);
        }

        public override TrackingState GetTrackingState() => TrackingState.Tracking;

        public override ScreenOrientation GetScreenOrientation() => ScreenOrientation.LandscapeLeft;

        public override bool TryGetCameraPose(out Matrix4x4 pose) => _cameraSubsystem.TryGetCameraPose(out pose);

        // Depth extrinsics on Meta Quest equal the head pose (depth sensor is aligned with headset reference).
        public override bool TryGetDepthPose(out Matrix4x4 extrinsics) => _cameraSubsystem.TryGetHeadPose(out extrinsics);

        public override bool TryGetDepthCameraIntrinsicsCStruct(out CameraIntrinsicsCStruct depthIntrinsics)
        {
            depthIntrinsics = default;

            // Depth is not available for apps developed with earlier versions of Unity.
#if !UNITY_6000_0_OR_NEWER
            return false;
#endif
            // The usual way to get the intrinsics for the depth image would be to
            // call TryGetFrame() on the subsystem. In ARF's occlusion subsystem for
            // Meta, TryGetFrame() also fetches the depth texture from OpenXR.
            // The occlusion manager calls this every frame. If attempted to fetch
            // the depth texture multiple times, OpenXR will yield errors. Thus, we
            // are prevented from acquiring the intrinsics using the subsystem on Meta
            // devices. Here, we rely on a helper script, that collects the intrinsics
            // from a layer above (occlusion manager).
            if (OcclusionIntrinsicsBootstrap.HasIntrinsics)
            {
                var intrinsics = OcclusionIntrinsicsBootstrap.Intrinsics;
                depthIntrinsics = new CameraIntrinsicsCStruct(
                    intrinsics.focalLength, intrinsics.principalPoint, intrinsics.resolution);

                return true;
            }

            return false;
        }

        public override bool TryGetDepthCpuImage(out LightshipCpuImage depthCpuImage,
            out LightshipCpuImage confidenceCpuImage)
        {
            // Defaults
            confidenceCpuImage = new LightshipCpuImage { Planes = Array.Empty<LightshipCpuImagePlane>() };
            depthCpuImage = new LightshipCpuImage { Planes = Array.Empty<LightshipCpuImagePlane>() };

            // Depth is not available for apps developed with earlier versions of Unity
#if !UNITY_6000_0_OR_NEWER
            return false;
#endif

            // Release the previous image if it exists
            if (_environmentDepthCpuImage.valid)
            {
                _environmentDepthCpuImage.Dispose();
            }

            // Try to acquire the new image
            if (OcclusionSubsystem is not { running: true } ||
                !OcclusionSubsystem.TryAcquireEnvironmentDepthCpuImageExt(
                    ref _environmentDepthTexture2D,
                    out _environmentDepthCpuImage))
            {
                return false;
            }

            // Return the PAM consumable format
            return _environmentDepthCpuImage.valid &&
                LightshipCpuImage.TryGetFromXRCpuImage(_environmentDepthCpuImage, out depthCpuImage);
        }
    }
}
