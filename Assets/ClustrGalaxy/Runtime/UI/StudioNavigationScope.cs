using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace ClustrGalaxy.UI
{
    /// <summary>Reserve right stick and WASD for editing; restore the previous UI action on close.</summary>
    public sealed class StudioNavigationScope : IDisposable
    {
        private readonly InputSystemUIInputModule module;
        private readonly InputActionReference previous;
        private InputAction navigation;
        private InputActionAsset ownedAsset;
        private InputActionReference reference;
        public StudioNavigationScope(InputSystemUIInputModule module)
        {
            this.module = module ?? throw new ArgumentNullException(nameof(module)); previous = module.move;
            ownedAsset = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = new InputActionMap("Studio"); ownedAsset.AddActionMap(map);
            navigation = map.AddAction("Focus",InputActionType.PassThrough,expectedControlLayout:"Vector2");
            navigation.AddBinding("<Gamepad>/dpad"); navigation.AddBinding("<Gamepad>/leftStick");
            navigation.AddCompositeBinding("2DVector").With("Up","<Keyboard>/upArrow").With("Down","<Keyboard>/downArrow")
                .With("Left","<Keyboard>/leftArrow").With("Right","<Keyboard>/rightArrow");
            reference = InputActionReference.Create(navigation); module.move = reference; navigation.Enable();
        }
        public void Dispose()
        {
            if (navigation == null) return;
            if (module != null && module.move == reference) module.move = previous;
            ownedAsset.Disable(); ownedAsset.actionMaps[0].Dispose(); navigation = null;
            UnityEngine.Object.Destroy(ownedAsset); ownedAsset = null;
            if (reference != null) UnityEngine.Object.Destroy(reference);
            reference = null;
        }
    }
}
