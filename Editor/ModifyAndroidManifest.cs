using System;
using System.Collections.Generic;
using Niantic.Lightship.MetaQuest.Runtime.Utilities;
using Unity.XR.Management.AndroidManifest.Editor;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR;

namespace Niantic.Lightship.MetaQuest.Editor
{
#if UNITY_EDITOR
    internal class ModifyAndroidManifest : OpenXRFeatureBuildHooks
    {
        public override int callbackOrder => 1;
        public override Type featureType => typeof(LightshipIntegrationFeature);
        protected override void OnPreprocessBuildExt(BuildReport report) { }

        protected override void OnPostGenerateGradleAndroidProjectExt(string path) { }

        protected override void OnPostprocessBuildExt(BuildReport report) { }

        protected override ManifestRequirement ProvideManifestRequirementExt()
        {
            var androidOpenXRSettings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            var elementsToAdd = new List<ManifestElement>();

            var arCameraFeature = androidOpenXRSettings.GetFeature<LightshipARCameraFeature>();
            if (arCameraFeature != null && arCameraFeature.enabled)
            {
                elementsToAdd.Add(new ManifestElement
                {
                    ElementPath = new List<string> {"manifest", "uses-permission"},
                    Attributes = new Dictionary<string, string>
                    {
                        {"name", CameraPermissionUtils.AndroidCameraPermission}, {"required", "true"},
                    }
                });

                elementsToAdd.Add(new ManifestElement
                {
                    ElementPath = new List<string> {"manifest", "uses-permission"},
                    Attributes = new Dictionary<string, string>
                    {
                        {"name", CameraPermissionUtils.HorizonOSCameraPermission}, {"required", "true"},
                    }
                });

                elementsToAdd.Add(new ManifestElement
                {
                    ElementPath = new List<string> {"manifest", "uses-feature"},
                    Attributes = new Dictionary<string, string>
                    {
                        {"name", "com.oculus.feature.PASSTHROUGH"}, {"required", "true"},
                    }
                });
            }

            return new ManifestRequirement
            {
                SupportedXRLoaders = new HashSet<Type> {typeof(OpenXRLoader)}, NewElements = elementsToAdd
            };
        }
    }
#endif // UNITY_EDITOR
}
