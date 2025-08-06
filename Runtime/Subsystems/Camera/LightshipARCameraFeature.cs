// Copyright 2022-2025 Niantic.

using System;
using System.Collections.Generic;
using Niantic.Lightship.MetaQuest.Runtime.Utilities;
using Unity.XR.CoreUtils;
using UnityEngine;
#if MODULE_URP_ENABLED
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.XR.OpenXR.Features;
#endif

namespace Niantic.Lightship.MetaQuest
{
    /// <summary>
    /// Enables AR Foundation passthrough support via Lightship for Meta Quest devices.
    /// </summary>
#if UNITY_EDITOR
    [OpenXRFeature(UiName = "Lightship Meta AR Camera (Passthrough)",
        BuildTargetGroups = new[] { BuildTargetGroup.Android, BuildTargetGroup.Standalone },
        Company = "Niantic Spatial Inc.",
        Desc = "Lightship camera image support on Meta Quest devices",
        OpenxrExtensionStrings = KOpenXRMetaPassthroughExtension,
        Category = FeatureCategory.Feature,
        FeatureId = FeatureId,
        Version = "3.14.0")
    ]
#endif

    public class LightshipARCameraFeature : OpenXRFeature
    {
        /// <summary>
        /// The feature id string. This is used to give the feature a well known id for reference.
        /// </summary>
        public const string FeatureId = "com.nianticlabs.lightship.features.meta.arfoundation-meta-camera";

        // The required OpenXR feature to render the camera passthrough
        internal const string KOpenXRMetaPassthroughExtension = "XR_FB_passthrough";

        private static bool VerifyExtensions()
        {
            bool isExtensionEnabled = OpenXRRuntime.IsExtensionEnabled(KOpenXRMetaPassthroughExtension);
            if (isExtensionEnabled)
            {
                return true;
            }

            Debug.LogError($"This OpenXR runtime failed to enable {KOpenXRMetaPassthroughExtension}. " +
                $"LightshipARCameraFeature will be disabled.");
            return false;
        }

        protected override bool OnInstanceCreate(ulong xrInstance) => VerifyExtensions();

        private static List<XRCameraSubsystemDescriptor> s_cameraDescriptors = new();

        protected override void OnSubsystemCreate()
        {
            // The initialization of the camera subsystem must be delayed until the camera
            // permission is granted. We create a weak reference to prevent the closure
            // from capturing the feature instance, in case the permission dialog never
            // completes.
            var featureRef = new WeakReference<LightshipARCameraFeature>(this);

            // Verify camera permissions
            CameraPermissionUtils.AskCameraPermission(granted =>
            {
                if (granted)
                {
                    if (featureRef.TryGetTarget(out LightshipARCameraFeature feature))
                    {
                        feature.CreateSubsystem<XRCameraSubsystemDescriptor, XRCameraSubsystem>(
                            s_cameraDescriptors, LightshipMetaOpenXRCameraSubsystem.SubsystemId);
                    }
                }
            });
        }

        protected override void OnSubsystemDestroy()
        {
            DestroySubsystem<XRCameraSubsystem>();
        }

#if UNITY_EDITOR
        protected override void GetValidationChecks(List<ValidationRule> rules, BuildTargetGroup targetGroup)
        {
            // Rule for disabling the original camera implementation by Meta (custom rule)
            rules.Add(new ValidationRule(this)
            {
                message = "Meta Quest: AR Camera (Passthrough) feature must be disabled for using the camera via Lightship.",
                checkPredicate = () =>
                {
                    var metaCameraFeature = OpenXRSettings.ActiveBuildTargetInstance
                        .GetFeature<UnityEngine.XR.OpenXR.Features.Meta.ARCameraFeature>();
                    var metaCameraFeatureEnabled = metaCameraFeature != null && metaCameraFeature.enabled;
                    return !metaCameraFeatureEnabled;
                },
                fixItAutomatic = true,
                fixItMessage = "Disable the Meta Quest: AR Camera (Passthrough) feature.",
                fixIt = () =>
                {
                    var metaCameraFeature = OpenXRSettings.ActiveBuildTargetInstance
                        .GetFeature<UnityEngine.XR.OpenXR.Features.Meta.ARCameraFeature>();
                    if (metaCameraFeature != null)
                        metaCameraFeature.enabled = false;
                },
                error = false
            });

            // Copied from UnityEngine.XR.OpenXR.Features.Meta.ARCameraFeature
            // Rule for the camera clear flags
            rules.Add(new ValidationRule(this)
            {
                message = "Passthrough requires Camera clear flags set to solid color with alpha value zero.",
                checkPredicate = () =>
                {
                    var xrOrigin = FindAnyObjectByType<XROrigin>();
                    if (xrOrigin == null || !xrOrigin.enabled) return true;

                    var camera = xrOrigin.Camera;
                    if (camera == null || camera.GetComponent<ARCameraManager>() == null) return true;

                    return camera.clearFlags == CameraClearFlags.SolidColor && Mathf.Approximately(camera.backgroundColor.a, 0);
                },
                fixItAutomatic = true,
                fixItMessage = "Set your XR Origin camera's Clear Flags to solid color with alpha value zero.",
                fixIt = () =>
                {
                    var xrOrigin = FindAnyObjectByType<XROrigin>();
                    if (xrOrigin != null || xrOrigin.enabled)
                    {
                        var camera = xrOrigin.Camera;
                        if (camera != null || camera.GetComponent<ARCameraManager>() != null)
                        {
                            camera.clearFlags = CameraClearFlags.SolidColor;
                            Color clearColor = camera.backgroundColor;
                            clearColor.a = 0;
                            camera.backgroundColor = clearColor;
                        }
                    }
                },
                error = false
            });

#if MODULE_URP_ENABLED
            // Copied from UnityEngine.XR.OpenXR.Features.Meta.ARCameraFeature
            // Rule for URP settings
            rules.Add(new ValidationRule(this)
            {
                message =
                    "Vulkan supports the most setting configurations to enable Passthrough on Meta Quest when using URP.",
                checkPredicate = () =>
                {
                    if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)
                    {
                        var graphicsApis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
                        return graphicsApis.Length > 0 && graphicsApis[0] == GraphicsDeviceType.Vulkan;
                    }

                    return true;
                },
                fixItAutomatic = true,
                fixItMessage =
                    "Go to Project Settings > Player Settings > Android. In the list of 'Graphics APIs', make sure that " +
                    "'Vulkan' is listed as the first API.",
                fixIt = () =>
                {
                    var currentGraphicsApis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
                    int apiLength = currentGraphicsApis.Length;
                    apiLength += Array.Exists(currentGraphicsApis, element => element == GraphicsDeviceType.Vulkan)
                        ? 0
                        : 1;
                    GraphicsDeviceType[] correctGraphicsApis = new GraphicsDeviceType[apiLength];
                    correctGraphicsApis[0] = GraphicsDeviceType.Vulkan;
                    var id = 1;
                    for (var i = 0; i < currentGraphicsApis.Length; ++i)
                    {
                        if (currentGraphicsApis[i] != GraphicsDeviceType.Vulkan)
                        {
                            correctGraphicsApis[id] = currentGraphicsApis[i];
                            id++;
                        }
                    }

                    PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, correctGraphicsApis);
                },
                error = false,
            });
#endif
        }
#endif
    }
}
