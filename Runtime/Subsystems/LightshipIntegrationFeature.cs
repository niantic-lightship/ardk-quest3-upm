using System.Collections.Generic;
using System.Linq;
using Niantic.Lightship.AR;
using Niantic.Lightship.AR.Core;
using Niantic.Lightship.AR.Loader;
using Niantic.Lightship.AR.Subsystems.Meshing;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.XRSubsystems;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR.Features;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.XR.OpenXR.Features;
#endif

namespace Niantic.Lightship.MetaQuest
{
#if UNITY_EDITOR
    [OpenXRFeature(
        UiName = "Lightship Meta Features Integration",
        BuildTargetGroups = new[] {BuildTargetGroup.Android},
        Company = "Niantic Spatial Inc.",
        Desc = "Necessary to deploy a Lightship app to a Meta Quest",
        Version = "3.14.0",
        Required = false,
        Priority = -1,
        Category = FeatureCategory.Feature,
        FeatureId = FeatureID
    )]
#endif

    public sealed class LightshipIntegrationFeature : OpenXRFeature, ILightshipInternalLoaderSupport
    {
        public const string FeatureID = "com.nianticlabs.lightship.features.meta.integration";
        private LightshipLoaderHelper _lightshipLoaderHelper;
        private readonly List<ILightshipExternalLoader> _externalLoaders = new();

        protected override void OnSubsystemCreate()
        {
            // Create the native subsystem loader
            NativeLoaderHelper nativeLoader = new MetaNativeLoaderHelper(this);

            // Create the playback loader helper if playback is enabled
            PlaybackLoaderHelper playbackLoader =
                LightshipSettingsHelper.ActiveSettings.UsePlayback ? new PlaybackLoaderHelper() : null;

            _lightshipLoaderHelper = new LightshipLoaderHelper(nativeLoader, playbackLoader);
            if (!_lightshipLoaderHelper.Initialize(this))
            {
                Log.Error("Could not initialize Lightship Meta support");
            }
        }

        public void AddExternalLoader(ILightshipExternalLoader loader) => _externalLoaders.Add(loader);

        public new void CreateSubsystem<TDescriptor, TSubsystem>(List<TDescriptor> descriptors, string id)
            where TDescriptor : ISubsystemDescriptor where TSubsystem : ISubsystem =>
            base.CreateSubsystem<TDescriptor, TSubsystem>(descriptors, id);

        public new void DestroySubsystem<T>() where T : class, ISubsystem => base.DestroySubsystem<T>();

        public T GetLoadedSubsystem<T>() where T : class, ISubsystem
        {
            var xrLoader = XRGeneralSettings.Instance.Manager.activeLoaders.FirstOrDefault();
            if (xrLoader == null)
            {
                Log.Error("Could not find XRLoader");
                return null;
            }

            return xrLoader.GetLoadedSubsystem<T>();
        }

        public bool InitializeWithLightshipHelper(LightshipLoaderHelper lightshipLoaderHelper) =>
            throw new System.NotImplementedException();

        public bool InitializePlatform() => true;

        public bool DeinitializePlatform() => true;

        public bool IsPlatformDepthAvailable() => true;
    }

    internal sealed class MetaNativeLoaderHelper : NativeLoaderHelper
    {
        private readonly ILightshipInternalLoaderSupport _internalLoader;

        public MetaNativeLoaderHelper(ILightshipInternalLoaderSupport internalLoader) =>
            _internalLoader = internalLoader;

        internal override bool Initialize(ILightshipInternalLoaderSupport loader, bool isLidarSupported) =>
            Initialize();

        private bool Initialize()
        {
            // Always use platform depth for Meta Quest
            const bool usePlatformDepth = true;

            var settings = LightshipSettingsHelper.ActiveSettings;
            LightshipUnityContext.Initialize(usePlatformDepth, settings.TestSettings.DisableTelemetry);

            // Substitute the semantics subsystem
            if (settings.UseLightshipSemanticSegmentation)
            {
                SubstituteSubsystem<XRSemanticsSubsystem, XRSemanticsSubsystemDescriptor>(SemanticsSubsystemDescriptors,
                    "Lightship-Semantics");
            }

            // Substitute the object detection subsystem
            if (settings.UseLightshipObjectDetection)
            {
                SubstituteSubsystem<XRObjectDetectionSubsystem, XRObjectDetectionSubsystemDescriptor>
                    (ObjectDetectionSubsystemDescriptors, "Lightship-ObjectDetection");
            }

            // Create Lightship Persistent Anchor subsystem
            if (settings.UseLightshipPersistentAnchor)
            {
                SubstituteSubsystem<XRPersistentAnchorSubsystem, XRPersistentAnchorSubsystemDescriptor>
                    (PersistentAnchorSubsystemDescriptors, "Lightship-PersistentAnchor");
            }

            // Substitute the meshing subsystem
            if (settings.UseLightshipMeshing)
            {
                // our C# "ghost" creates our meshing module to listen to Unity meshing lifecycle callbacks
                _internalLoader.DestroySubsystem<XRMeshSubsystem>();
                LightshipMeshingProvider.Construct(LightshipUnityContext.UnityContextHandle);

                // Create the Unity integrated subsystem
                _internalLoader.CreateSubsystem<XRMeshSubsystemDescriptor, XRMeshSubsystem>
                (
                    MeshingSubsystemDescriptors,
                    "LightshipMeshing"
                );
            }

            return true;
        }

        /// <summary>
        /// Substitutes the given subsystem with a Lightship implementation.
        /// </summary>
        private void SubstituteSubsystem<TSubsystem, TDescriptor>(List<TDescriptor> descriptors, string id)
            where TSubsystem : class, ISubsystem
            where TDescriptor : ISubsystemDescriptor
        {
            // Destroy the existing subsystem
            _internalLoader.DestroySubsystem<TSubsystem>();

            // Create the new subsystem
            _internalLoader.CreateSubsystem<TDescriptor, TSubsystem>(descriptors, id);
            Log.Info("Created subsystem with id " + id);
        }
    }
}
