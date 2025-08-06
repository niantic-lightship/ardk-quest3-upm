// Copyright 2022-2025 Niantic.

using UnityEngine;

namespace Niantic.Lightship.MetaQuest.Runtime.Utilities
{
    /// <summary>
    /// Utility class to check if the current headset supports Passthrough Camera API
    /// </summary>
    internal static class CameraSupport
    {
        // The Horizon OS starts supporting PCA with v72. However, the behaviour of the feature is slightly different
        // in this version compared to v74.
        private const int EarlySupportOsVersion = 72;

        // Helpers
        private static bool? s_isSupported;
        private static int? s_horizonOsVersion;

        /// <summary>
        /// Returns true if the current headset supports Passthrough Camera API.
        /// </summary>
        private static bool IsSupported
        {
            get
            {
                if (!s_isSupported.HasValue)
                {
                    var headset = OVRPlugin.GetSystemHeadsetType();
                    return (headset == OVRPlugin.SystemHeadset.Meta_Quest_3 ||
                            headset == OVRPlugin.SystemHeadset.Meta_Quest_3S) &&
                        HorizonOSVersion >= EarlySupportOsVersion;
                }

                return s_isSupported.Value;
            }
        }

        /// <summary>
        /// Get the Horizon OS version number on the headset.
        /// </summary>
        private static int HorizonOSVersion
        {
            get
            {
                if (!s_horizonOsVersion.HasValue)
                {
                    var vrosClass = new AndroidJavaClass("vros.os.VrosBuild");
                    s_horizonOsVersion = vrosClass.CallStatic<int>("getSdkVersion");

                    // 10000 is a special OS built on top of v72 and containing additional fixes to Passthrough Camera API.
                    // But this is still v72.
                    if (s_horizonOsVersion == 10000)
                    {
                        s_horizonOsVersion = 72;
                    }
                }

                return s_horizonOsVersion.Value;
            }
        }

        /// <summary>
        /// Whether the running OS is an early version of Horizon OS that first supports the Passthrough Camera API.
        /// </summary>
        public static bool IsEarlyVersion
        {
            get => HorizonOSVersion == EarlySupportOsVersion;
        }
    }
}
