using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.XR.OpenXR.Features;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.XR.OpenXR.Features.Meta;
#endif // UNITY_EDITOR

namespace Niantic.Lightship.MetaQuest.Editor
{
#if UNITY_EDITOR
    [OpenXRFeatureSet(
        FeatureIds = new[]
        {
            LightshipIntegrationFeature.FeatureID, LightshipARCameraFeature.FeatureId
        },
        DefaultFeatureIds = new[]
        {
            LightshipIntegrationFeature.FeatureID, LightshipARCameraFeature.FeatureId
        },
        RequiredFeatureIds = new[] { LightshipIntegrationFeature.FeatureID, MetaQuestFeature.featureId },
        UiName = "Niantic Lightship support for Meta Quest",
        Description = "Features to use Lightship functionality on the Meta Quest.",
        FeatureSetId = Constants.LightshipFeatureSetId,
        SupportedBuildTargets = new[] { BuildTargetGroup.Android }
    )]
    internal class LightshipMetaOpenXrFeatureSet { }

    internal static class Constants
    {
        public const string MetaFeatureSetId = "com.unity.openxr.featureset.meta";
        public const string LightshipFeatureSetId = "com.nianticlabs.lightship.featureset.meta";
    }

    internal static class MenuItems
    {
        [MenuItem("Lightship/Run Setup For Meta")]
        private static void SetupMeta()
        {
            // Build and player settings
            BuildSymbolsUtils.Add("NIANTIC_LIGHTSHIP_META_ENABLED");
#if MODULE_SHAREDAR_ENABLED
            BuildSymbolsUtils.Add("NIANTIC_LIGHTSHIP_SHAREDAR_ENABLED");
#else
            BuildSymbolsUtils.Remove("NIANTIC_LIGHTSHIP_SHAREDAR_ENABLED");
#endif
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] {GraphicsDeviceType.Vulkan});

            // Enable the Meta feature group
            var featureSet = OpenXRFeatureSetManager.GetFeatureSetWithId(BuildTargetGroup.Android, Constants.MetaFeatureSetId);
            if (featureSet != null)
            {
                featureSet.isEnabled = true;
            }
            else
            {
                Debug.LogError($"FeatureSet {Constants.MetaFeatureSetId} not found.");
            }

            // Enable Lightship feature group
            var featureGroup = OpenXRFeatureSetManager.GetFeatureSetWithId(BuildTargetGroup.Android, Constants.LightshipFeatureSetId);
            if (featureGroup != null)
            {
                featureGroup.isEnabled = true;
            }
            else
            {
                Debug.LogError($"FeatureSet {Constants.LightshipFeatureSetId} not found.");
            }

            // Check individual features for android
            var oxrSettings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);

            // Check for Meta Quest support
            var metaXrSettings = oxrSettings.GetFeature<MetaQuestFeature>();
            if (metaXrSettings != null)
            {
                metaXrSettings.enabled = true;
            }
            else
            {
                Debug.LogError("Couldn't find MetaQuestFeature in OpenXR settings");
            }

            // Check for lightship integration
            var integration = oxrSettings.GetFeature<LightshipIntegrationFeature>();
            if (integration != null)
            {
                integration.enabled = true;
            }
            else
            {
                Debug.LogError("Couldn't find LightshipIntegrationFeature in OpenXR settings");
            }

            // Disable the default camera feature for Meta
            var metaCamera = oxrSettings.GetFeature<ARCameraFeature>();
            if (metaCamera != null)
            {
                metaCamera.enabled = false;
            }

            // Enable the lightship camera feature for Meta
            var lsCamera = oxrSettings.GetFeature<LightshipARCameraFeature>();
            if (lsCamera != null)
            {
                lsCamera.enabled = true;
            }
            else
            {
                Debug.LogError("Couldn't find LightshipARCameraFeature in OpenXR settings");
            }

            // Check individual features
            var features = FeatureHelpers.GetFeaturesWithIdsForBuildTarget(BuildTargetGroup.Android, featureSet.featureIds);
            foreach (var feature in features)
            {
                // Enable everything except the ARCameraFeature
                var isFeatureReplaced = feature is ARCameraFeature;
                var isFeatureCompatible = feature is not BoundaryVisibilityFeature;
                feature.enabled = !isFeatureReplaced && isFeatureCompatible;
            }

            // Add the Lightship semantics overlay to the always included shaders
            AddAlwaysIncludedShader(FindShaderAsset("UnpackDepth"));
            AddAlwaysIncludedShader(FindShaderAsset("OcclusionMeshStereo"));
            AddAlwaysIncludedShader(FindShaderAsset("LightshipSemanticsOverlay", local: false));
        }

        private static void AddAlwaysIncludedShader(Shader shader)
        {
            if (shader == null)
            {
                return;
            }

            // Get the Graphics Settings asset
            var graphicsSettingsObj = AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
            if (graphicsSettingsObj == null)
            {
                Debug.LogError("GraphicsSettings.asset not found!");
                return;
            }

            // Use SerializedObject to access alwaysIncludedShaders
            var so = new SerializedObject(graphicsSettingsObj);
            var shadersProp = so.FindProperty("m_AlwaysIncludedShaders");

            // Check if the shader is already in the list
            bool alreadyIncluded = false;
            for (int i = 0; i < shadersProp.arraySize; i++)
            {
                var prop = shadersProp.GetArrayElementAtIndex(i);
                if (prop.objectReferenceValue == shader)
                {
                    alreadyIncluded = true;
                    break;
                }
            }

            if (!alreadyIncluded)
            {
                int newIndex = shadersProp.arraySize;
                shadersProp.InsertArrayElementAtIndex(newIndex);
                shadersProp.GetArrayElementAtIndex(newIndex).objectReferenceValue = shader;
                so.ApplyModifiedProperties();
                Debug.Log($"Shader '{shader.name}' added to Always Included Shaders.");
            }
        }

        private static Shader FindShaderAsset(string fileName, bool local = true)
        {
            // Search for all shaders in the package
            var root = new[]{"Packages/com.nianticlabs.lightship.metaquest/Assets/Shaders/"};
            string[] guids = local
                ? AssetDatabase.FindAssets("t:Shader " + fileName, root)
                : AssetDatabase.FindAssets("t:Shader " + fileName);
            if (guids.Length == 0)
            {
                return null;
            }

            // Find the first shader with an exact name match (or use the first result)
            Shader foundShader = null;
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (shader != null)
                {
                    foundShader = shader;
                    break;
                }
            }

            return foundShader;
        }

        private static class BuildSymbolsUtils
        {
            public static void Add(string define)
            {
                // Get a set of existing defines
                string definesString = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android);
                var allDefines = new HashSet<string>(definesString.Split(';'));

                // Add the new define if it doesn't already exist
                if (allDefines.Add(define))
                {
                    PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, string.Join(";", allDefines));
                }
            }

            public static void Remove(string define)
            {
                // Get a set of existing defines
                string definesString = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android);
                var allDefines = new HashSet<string>(definesString.Split(';'));

                // Remove the define if it exists
                if (allDefines.Remove(define))
                {
                    PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, string.Join(";", allDefines));
                }
            }
        }
    }
#endif // UNITY_EDITOR
}
