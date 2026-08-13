using System;
using System.Collections.Generic;
using ClustrGalaxy.Simulation;
using UnityEngine;

namespace ClustrGalaxy.World
{
    public sealed class GalaxyEntityPool
    {
        private readonly Transform poolRoot;
        private readonly Dictionary<PrimitiveType, Stack<GameObject>> available = new();

        public GalaxyEntityPool(Transform owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            poolRoot = new GameObject("Galaxy Entity Pool").transform;
            poolRoot.SetParent(owner, false);
        }

        public GameObject Acquire(PrimitiveType shape, Transform parent)
        {
            if (!available.TryGetValue(shape, out var objects))
            {
                objects = new Stack<GameObject>();
                available.Add(shape, objects);
            }

            GameObject body;
            if (objects.Count > 0)
            {
                body = objects.Pop();
            }
            else
            {
                body = GameObject.CreatePrimitive(shape);
                body.AddComponent<GalaxyEntity>();
                body.AddComponent<LiveEntityMetadata>();
                body.AddComponent<PooledGalaxyObject>().Shape = shape;
                body.SetActive(false);
            }

            body.transform.SetParent(parent, false);
            body.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            body.transform.localScale = Vector3.one;
            body.SetActive(true);
            return body;
        }

        public void Release(GameObject body)
        {
            var marker = body != null ? body.GetComponent<PooledGalaxyObject>() : null;
            if (marker == null || !body.activeSelf)
            {
                return;
            }

            body.SetActive(false);
            body.GetComponent<GalaxyEntity>().ClearIdentity();
            body.GetComponent<LiveEntityMetadata>().Clear();
            DisableOptional<KeplerOrbit>(body);
            DisableOptional<GravitySource>(body);
            body.transform.SetParent(poolRoot, false);
            if (!available.TryGetValue(marker.Shape, out var objects))
            {
                objects = new Stack<GameObject>();
                available.Add(marker.Shape, objects);
            }
            objects.Push(body);
        }

        public void ReleaseDescendants(Transform root)
        {
            if (root == null) return;
            var pooled = root.GetComponentsInChildren<PooledGalaxyObject>(includeInactive: false);
            for (var index = 0; index < pooled.Length; index++)
            {
                Release(pooled[index].gameObject);
            }
        }

        private static void DisableOptional<T>(GameObject body) where T : Behaviour
        {
            var component = body.GetComponent<T>();
            if (component != null) component.enabled = false;
        }
    }

    public sealed class PooledGalaxyObject : MonoBehaviour
    {
        public PrimitiveType Shape { get; set; }
    }
}
