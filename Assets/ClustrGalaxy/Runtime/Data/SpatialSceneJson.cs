using System;
using UnityEngine;

namespace ClustrGalaxy.Data
{
    public static class SpatialSceneJson
    {
        public static SpatialScene ParseScene(string json)
        {
            var scene = JsonUtility.FromJson<SpatialScene>(json) ?? new SpatialScene();
            scene.nodes ??= Array.Empty<SpatialNode>();
            scene.links ??= Array.Empty<SpatialLink>();
            scene.next_cursor ??= string.Empty;
            return scene;
        }
    }
}
