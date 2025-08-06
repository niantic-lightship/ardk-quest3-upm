using UnityEditor;
using Unity.XR.CoreUtils.Editor;
using UnityEngine.Rendering;
#if MODULE_URP_ENABLED
using UnityEngine.Rendering.Universal;
#endif

namespace Niantic.Lightship.MetaQuest.Editor
{
#if UNITY_EDITOR
    /// <summary>
    /// Global project validation rules for the Lightship Meta Quest package.
    /// </summary>
    internal static class ProjectValidationRules
    {
        private const string KCategory = "Lightship Meta Quest Support";

        [InitializeOnLoadMethod]
        private static void RegisterValidationRules()
        {
            var rules = new[]
            {
                // The project must use the new input system package
                new BuildValidationRule
                {
                    Category = KCategory,
                    Message =
                        "Lightship requires the Active Input Handling set to 'Input System Package (New)' in Player Settings.",
                    IsRuleEnabled = () => true,
                    CheckPredicate = () =>
                    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
                        return true;
#else
                        return false;
#endif
                    },
                    FixItMessage =
                        "Open Project Settings > Player Settings > Set Active Input Handling to 'Input System Package (New).",
                    Error = true
                },

#if MODULE_URP_ENABLED
                // Enable render graph compatibility mode
                new BuildValidationRule
                {
                    Category = KCategory,
                    Message =
                        "Consider enabling compatibility mode when using the Universal Rendering Pipeline.",
                    IsRuleEnabled = () => true,
                    CheckPredicate = () =>
                    {
                        var settings = GraphicsSettings.GetRenderPipelineSettings<RenderGraphSettings>();
                        return settings == null || settings.enableRenderCompatibilityMode;
                    },
					FixIt = () =>
                    {
                        var settings = GraphicsSettings.GetRenderPipelineSettings<RenderGraphSettings>();
                        if (settings != null)
                        {
                            settings.enableRenderCompatibilityMode = true;
                        }
                    },
                    FixItMessage =
                        "Open Project Settings > Graphics > Render Graph > Enable Compatibility Mode.",
                    Error = false,
                },
#endif
            };

            // Register rules
            BuildValidator.AddRules(BuildTargetGroup.Android, rules);
        }
    }
#endif
}
