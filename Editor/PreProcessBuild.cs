using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Niantic.Lightship.MetaQuest.Editor
{
    namespace Niantic.Lightship.MetaQuest.Editor
    {
        public class PreProcessBuild : IPreprocessBuildWithReport
        {
            public int callbackOrder => 0;

            public void OnPreprocessBuild(BuildReport report)
            {
                // Always included shaders
                AddBackgroundShaderToProject("Hidden/UnpackDepth");
            }

            /// <summary>
            /// Adds a background shader with the given name to the project as a preloaded asset.
            /// </summary>
            /// <param name="shaderName">The name of a shader to add to the project.</param>
            /// <exception cref="UnityEditor.Build.BuildFailedException">Thrown if a shader with the given name cannot be
            /// found.</exception>
            private static void AddBackgroundShaderToProject(string shaderName)
            {
                if (string.IsNullOrEmpty(shaderName))
                {
                    Debug.LogWarning("Incompatible render pipeline in GraphicsSettings.currentRenderPipeline. Background "
                        + "rendering may not operate properly.");
                }
                else
                {
                    Shader shader = FindShaderOrFailBuild(shaderName);

                    Object[] preloadedAssets = PlayerSettings.GetPreloadedAssets();

                    var shaderAssets = (from preloadedAsset in preloadedAssets where shader.Equals(preloadedAsset)
                        select preloadedAsset);
                    if ((shaderAssets == null) || !shaderAssets.Any())
                    {
                        List<Object> preloadedAssetsList = preloadedAssets.ToList();
                        preloadedAssetsList.Add(shader);
                        PlayerSettings.SetPreloadedAssets(preloadedAssetsList.ToArray());
                    }
                }
            }

            /// <summary>
            /// Finds a shader with the given name. If no shader with that name is found, the build fails.
            /// </summary>
            /// <param name="shaderName">The name of a shader to find.</param>
            /// <returns>
            /// The shader with the given name.
            /// </returns>
            /// <exception cref="UnityEditor.Build.BuildFailedException">Thrown if a shader with the given name cannot be
            /// found.</exception>
            private static Shader FindShaderOrFailBuild(string shaderName)
            {
                var shader = Shader.Find(shaderName);
                if (shader == null)
                {
                    throw new BuildFailedException($"Cannot find shader '{shaderName}'");
                }

                return shader;
            }
        }
    }
}
