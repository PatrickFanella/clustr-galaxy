using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace ClustrGalaxy.UI
{
    /// <summary>Opt-in virtual-controller UI evidence in the real Linux player.</summary>
    public sealed class GalaxyUiSmokeRunner : MonoBehaviour
    {
        private Gamepad controller;
        private ProbeHud hud;
        private InputSettings.BackgroundBehavior previousBackground;
        private IEnumerator Start()
        {
            if (!Array.Exists(Environment.GetCommandLineArgs(), value => value == "--clustr-ui-smoke"))
            { enabled = false; yield break; }
            hud = GetComponent<ProbeHud>();
            previousBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            controller = InputSystem.AddDevice<Gamepad>();
            yield return new WaitForSecondsRealtime(3);
            yield return Press(GamepadButton.Start);
            if (!hud.InputCaptured) { Fail("Virtual controller could not open Actions"); yield break; }
            yield return new WaitForSecondsRealtime(.5f);
            var directory = Path.Combine(Application.persistentDataPath,"ClustrGalaxy","ui-smoke");
            Directory.CreateDirectory(directory);
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"actions.png"));
            yield return new WaitForSecondsRealtime(1);
            yield return Press(GamepadButton.East);
            if (hud.InputCaptured) { Fail("Virtual controller could not return to flight"); yield break; }
            File.WriteAllText(Path.Combine(directory,"result.json"),
                "{\"scenario\":\"virtual-controller-actions-back\",\"passed\":true,\"real_controller\":false}");
            Debug.Log("[ClustrGalaxy.UiSmoke] PASS virtual-controller-actions-back evidence=" + directory);
            yield return new WaitForSecondsRealtime(1);
            Application.Quit(0);
        }
        private IEnumerator Press(GamepadButton button)
        {
            InputSystem.QueueStateEvent(controller,new GamepadState().WithButton(button));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(controller,new GamepadState());
            yield return null;
        }
        private static void Fail(string reason)
        { Debug.LogError("[ClustrGalaxy.UiSmoke] FAIL " + reason); Application.Quit(2); }
        private void OnDestroy()
        {
            if (controller == null) return;
            InputSystem.RemoveDevice(controller);
            InputSystem.settings.backgroundBehavior = previousBackground;
        }
    }
}
