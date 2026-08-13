using System;
using ClustrGalaxy.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ClustrGalaxy.Interaction
{
    public sealed class ProbeTargeting : MonoBehaviour
    {
        [SerializeField] private float maximumAngle = 18f;
        [SerializeField] private float maximumDistance = 30000f;

        private InputAction targetAction = null!;
        public GalaxyEntity Target { get; private set; }
        public event Action<GalaxyEntity> TargetChanged = delegate { };

        private void Awake()
        {
            targetAction = new InputAction("Target", InputActionType.Button, "<Keyboard>/f");
            targetAction.AddBinding("<Gamepad>/buttonSouth");
            targetAction.performed += _ => AcquireTarget();
        }

        private void OnEnable() => targetAction?.Enable();
        private void OnDisable() => targetAction?.Disable();

        public void AcquireTarget()
        {
            var directHit = DirectReticleTarget();
            var observer = Camera.main != null ? Camera.main.transform : transform;
            GalaxyEntity best = null;
            var bestScore = float.PositiveInfinity;
            foreach (var entity in GalaxyEntity.ActiveEntities)
            {
                var offset = entity.transform.position - observer.position;
                var distance = offset.magnitude;
                if (distance <= 0.01f || distance > maximumDistance)
                {
                    continue;
                }

                var angle = Vector3.Angle(observer.forward, offset);
                if (angle > maximumAngle)
                {
                    continue;
                }

                var score = ProbeTargetingModel.Score(
                    angle, distance, directHit != null && entity == directHit);
                if (score < bestScore)
                {
                    best = entity;
                    bestScore = score;
                }
            }

            Target = best;
            TargetChanged(Target);
        }

        private GalaxyEntity DirectReticleTarget()
        {
            var observer = Camera.main != null ? Camera.main.transform : transform;
            if (!Physics.Raycast(observer.position, observer.forward, out var hit, maximumDistance))
            {
                return null;
            }

            return hit.collider.GetComponentInParent<GalaxyEntity>();
        }

        private void OnDestroy()
        {
            targetAction?.Dispose();
        }
    }
}
