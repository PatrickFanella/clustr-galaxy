using UnityEngine;

namespace ClustrGalaxy.UI
{
    public static class ComponentLookup
    {
        /// <summary>
        /// Existing component or a new one. In the Editor, GetComponent returns a Unity "fake null"
        /// for a missing component, which C#'s ?? treats as present; TryGetComponent does not.
        /// </summary>
        public static T GetOrAddComponent<T>(this Component owner) where T : Component =>
            owner.TryGetComponent<T>(out var existing) ? existing : owner.gameObject.AddComponent<T>();
    }
}
