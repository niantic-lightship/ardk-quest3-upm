using Niantic.Lightship.AR;
using UnityEngine.InputSystem.Layouts;

namespace Niantic.Lightship.MetaQuest
{
    /// <summary>
    /// The default OpenXR head tracking device does not differentiate between the center and left eye.
    /// We deploy this custom input device to provide the correct eye poses required when processing
    /// images captured from the device camera.
    /// </summary>
    [InputControlLayout(
        stateType = typeof(LightshipInputState),
        displayName = ProductName)]
    internal sealed class LightshipMetaOpenXRCameraDevice : LightshipInputDevice
    {
        public const string ProductName = "LightshipMetaOpenXRCameraDevice";
    }
}
