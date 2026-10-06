using System;
using ClustrGalaxy.Bootstrap;
using ClustrGalaxy.Core.Activities;
using ClustrGalaxy.Core.Survey;
using ClustrGalaxy.World;
using UnityEngine;

namespace ClustrGalaxy.UI
{
    public sealed class ExplorationCelebrations : MonoBehaviour
    {
        private sealed class Slot
        {
            public GameObject Root;
            public readonly LineRenderer[] Lines = new LineRenderer[16];
            public readonly Transform[] Starts = new Transform[16];
            public readonly Transform[] Ends = new Transform[16];
            public readonly GalaxyEntity[] StartEntities=new GalaxyEntity[16],EndEntities=new GalaxyEntity[16];
            public Material Material;
            public readonly string[] StartIds = new string[16];
            public readonly string[] EndIds = new string[16];
            public CelebrationKind Kind;
            public int Count;
        }
        private readonly Slot[] slots = new Slot[3];
        private readonly CelebrationSlots clock = new();
        private readonly Vector3[] recentFlight = new Vector3[32];
        private int sampleCount, sampleCursor;
        private float nextSample;
        private GalaxySessionBehaviour owner;
        private GalaxySurveyVisibilityController visibility;
        private FloatingOrigin origin;
        private Material material;
        private Camera flightCamera;
        private bool ready;
        public int ActiveCount { get; private set; }
        public bool TryGetActivePosition(int index,out Vector3 position)
        {position=default;if(index<0||index>=slots.Length||slots[index]?.Root==null||!slots[index].Root.activeSelf)return false;position=slots[index].Root.transform.position;return true;}
        public void Initialize(GalaxySessionBehaviour session)
        {
            owner = session; visibility = GetComponent<GalaxySurveyVisibilityController>(); origin = GetComponent<FloatingOrigin>();
            flightCamera = GetComponentInChildren<Camera>();
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            if (shader == null) { Debug.LogWarning("[ClustrGalaxy.Celebrations] Visual effects unavailable; progress remains available."); return; }
            material = new Material(shader); material.EnableKeyword("_EMISSION");
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor",Color.white);
            if (material.HasProperty("_Color")) material.SetColor("_Color",Color.white);
            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i] = new Slot { Root = new GameObject("Pooled celebration " + i), Material = new Material(material) };
                for (var line = 0; line < slot.Lines.Length; line++)
                {
                    var go = new GameObject("Celebration stroke"); go.transform.SetParent(slot.Root.transform,false);
                    var renderer = slot.Lines[line] = go.AddComponent<LineRenderer>();
                    renderer.sharedMaterial = slot.Material; renderer.useWorldSpace = false; renderer.positionCount = 32;
                    renderer.startWidth = renderer.endWidth = 6; renderer.numCapVertices = 2; go.SetActive(false);
                }
                slot.Root.SetActive(false);
            }
            visibility.SurveyTransitionCommitted += Committed;
            owner.Modes.WorldModeChanged += ModeChanged;
            if (origin != null) origin.Rebased += Rebased;
            ready = true;
        }
        private void ModeChanged(ClustrGalaxy.Gameplay.GalaxyWorldMode _)
        { foreach (var slot in slots) if (slot?.Root != null) slot.Root.SetActive(false); }
        private void Rebased(Vector3 shift, DoubleVector3 _)
        { for (var i = 0; i < sampleCount; i++) recentFlight[i] += shift; }
        private void Committed(SurveyTransitionOutcome outcome)
        {
            if (!outcome.Succeeded || !outcome.Changed) return;
            foreach (var cue in outcome.Celebrations) Show(cue);
        }
        public void Show(CelebrationCue cue)
        {
            if (!ready || owner?.Session?.Activities == null || owner.Session.Activities.State.Preferences.CelebrationIntensity <= 0) return;
            var index = clock.TryStart(cue.Id,Time.unscaledTimeAsDouble); if (index < 0) return;
            var slot = slots[index]; slot.Root.transform.position = Vector3.zero;slot.Root.transform.localScale=Vector3.one; slot.Kind = cue.Kind; slot.Count = 0;
            for (var i = 0; i < slot.Lines.Length; i++) { slot.Lines[i].gameObject.SetActive(false); slot.Starts[i] = slot.Ends[i] = null;slot.StartEntities[i]=slot.EndEntities[i]=null; }
            if (cue.Kind == CelebrationKind.SystemRoutes && cue.Entities.Count > 0)
            {
                foreach (var route in owner.Registry.Routes)
                {
                    if (slot.Count == slot.Lines.Length) break;
                    if (!route.gameObject.activeInHierarchy || route.SourceId != cue.Entities[0].Value) continue;
                    var from = owner.Registry.FindEntity(route.SourceId); var to = owner.Registry.FindEntity(route.TargetId);
                    if (from != null && to != null && visibility.IsKnown(from) && visibility.IsKnown(to)) AddSegment(slot,from.transform,to.transform);
                }
            }
            else if (cue.Kind == CelebrationKind.Constellation)
            {
                for (var i = 0; i < cue.Entities.Count; i++)
                {
                    var from = owner.Registry.FindEntity(cue.Entities[i].Value);
                    var to = owner.Registry.FindEntity(cue.Entities[(i+1)%cue.Entities.Count].Value);
                    if (from != null && to != null && visibility.CanLabel(from) && visibility.CanLabel(to)) AddSegment(slot,from.transform,to.transform);
                }
                if (cue.Entities.Count > 0) GetComponent<ExplorationFeedback>()?.Audition(cue.Entities[0]);
            }
            else if (cue.Kind == CelebrationKind.FlightRibbon && sampleCount >= 2)
            {
                var line = slot.Lines[0]; line.positionCount = sampleCount;
                for (var i = 0; i < sampleCount; i++) line.SetPosition(i,recentFlight[(sampleCursor-sampleCount+i+recentFlight.Length)%recentFlight.Length]);
                line.gameObject.SetActive(true); slot.Count = 1;
            }
            else if (cue.Kind == CelebrationKind.Discovery && cue.Entities.Count > 0)
            {
                var entity = owner.Registry.FindEntity(cue.Entities[0].Value);
                if (entity != null && visibility.CanLabel(entity))
                {
                    slot.Root.transform.position = entity.transform.position;slot.Starts[0]=entity.transform;slot.StartEntities[0]=entity;slot.StartIds[0]=entity.StableId;
                    GetComponent<ExplorationFeedback>()?.Audition(cue.Entities[0]);
                    var radius = Mathf.Max(20,entity.transform.lossyScale.x*.7f); var line = slot.Lines[0]; line.positionCount = 32;
                    for (var i = 0; i < 32; i++) { var angle = i*Mathf.PI*2/31; line.SetPosition(i,new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius); }
                    line.gameObject.SetActive(true); slot.Count = 1;
                }
            }
            slot.Root.SetActive(slot.Count > 0);
        }
        private static void AddSegment(Slot slot,Transform from,Transform to)
        {
            var index = slot.Count++; slot.Starts[index] = from; slot.Ends[index] = to;
            slot.StartEntities[index]=from.GetComponent<GalaxyEntity>();slot.EndEntities[index]=to.GetComponent<GalaxyEntity>();
            slot.StartIds[index] = slot.StartEntities[index]?.StableId; slot.EndIds[index] = slot.EndEntities[index]?.StableId;
            slot.Lines[index].positionCount = 2; slot.Lines[index].gameObject.SetActive(true);
            slot.Lines[index].SetPosition(0,slot.Root.transform.InverseTransformPoint(from.position));
            slot.Lines[index].SetPosition(1,slot.Root.transform.InverseTransformPoint(to.position));
        }
        private void Update()
        {
            if (!ready || owner?.Session == null) return;
            if (Time.unscaledTime >= nextSample)
            {
                nextSample = Time.unscaledTime + .1f; recentFlight[sampleCursor] = transform.position;
                sampleCursor = (sampleCursor+1)%recentFlight.Length; sampleCount = Math.Min(sampleCount+1,recentFlight.Length);
            }
            ActiveCount = 0; var preferences = owner.Session.Activities?.State.Preferences;
            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i]; if (!slot.Root.activeSelf) continue;
                var remaining = (float)clock.Remaining(i,Time.unscaledTimeAsDouble);
                if (remaining <= 0 || preferences?.CelebrationIntensity == 0) { slot.Root.SetActive(false); continue; }
                if(slot.Kind==CelebrationKind.Discovery)
                {
                    var anchor=slot.Starts[0];var entity=slot.StartEntities[0];
                    if(entity==null||entity.StableId!=slot.StartIds[0]||!visibility.CanLabel(entity)){slot.Root.SetActive(false);continue;}
                    slot.Root.transform.position=anchor.position;slot.Root.transform.localScale=Vector3.one*(preferences?.ReducedMotion==true?1:Mathf.Lerp(.85f,1.15f,1-remaining*.5f));
                }
                ActiveCount++;
                var intensity = (preferences?.CelebrationIntensity ?? .6f) * Mathf.Min(1,remaining);
                var tint=slot.Kind switch{CelebrationKind.Discovery=>new Color(.96f,.77f,.42f),CelebrationKind.Constellation=>new Color(.76f,.69f,.96f),CelebrationKind.FlightRibbon=>new Color(.95f,.71f,.63f),_=>new Color(.43f,.86f,.84f)};
                var color = new Color(tint.r*intensity,tint.g*intensity,tint.b*intensity,1);
                if (slot.Material.HasProperty("_BaseColor")) slot.Material.SetColor("_BaseColor",color);
                if (slot.Material.HasProperty("_Color")) slot.Material.SetColor("_Color",color);
                if (slot.Material.HasProperty("_EmissionColor")) slot.Material.SetColor("_EmissionColor",color * 2);
                var width = preferences?.ReducedMotion == true ? 6 : 6 + Mathf.Sin(remaining*8)*2;
                for (var line = 0; line < slot.Count; line++)
                {
                    var renderer = slot.Lines[line]; renderer.startColor = renderer.endColor = color; renderer.startWidth = renderer.endWidth = width;
                    // A fixed world-space stroke becomes subpixel at overview distances.
                    // Keep a modest pixel floor while retaining the authored close-up width.
                    if (flightCamera != null && renderer.positionCount > 0)
                    {
                        var lineTransform = renderer.transform;
                        renderer.startWidth = VisibleStrokeWidth(lineTransform.TransformPoint(renderer.GetPosition(0)), width);
                        renderer.endWidth = VisibleStrokeWidth(lineTransform.TransformPoint(renderer.GetPosition(renderer.positionCount - 1)), width);
                    }
                    if (slot.Kind != CelebrationKind.SystemRoutes && slot.Kind != CelebrationKind.Constellation) continue;
                    if (slot.Starts[line] == null || slot.Ends[line] == null ||
                        !slot.Starts[line].gameObject.activeInHierarchy || !slot.Ends[line].gameObject.activeInHierarchy ||
                        slot.StartEntities[line]?.StableId != slot.StartIds[line] ||
                        slot.EndEntities[line]?.StableId != slot.EndIds[line])
                    { renderer.gameObject.SetActive(false); continue; }
                    renderer.SetPosition(0,slot.Root.transform.InverseTransformPoint(slot.Starts[line].position));
                    var start=slot.Root.transform.InverseTransformPoint(slot.Starts[line].position);var end=slot.Root.transform.InverseTransformPoint(slot.Ends[line].position);
                    var reveal=slot.Kind==CelebrationKind.Constellation&&preferences?.ReducedMotion!=true?Mathf.Clamp01((1-remaining*.5f)*slot.Count*1.5f-line):1;
                    renderer.SetPosition(1,Vector3.Lerp(start,end,reveal));
                }
            }
        }
        private float VisibleStrokeWidth(Vector3 worldPosition, float authoredWidth)
        {
            var depth = Mathf.Max(flightCamera.nearClipPlane,
                Vector3.Dot(worldPosition - flightCamera.transform.position, flightCamera.transform.forward));
            var verticalSpan = flightCamera.orthographic ? flightCamera.orthographicSize * 2 :
                2 * depth * Mathf.Tan(flightCamera.fieldOfView * Mathf.Deg2Rad * .5f);
            return Mathf.Max(authoredWidth, verticalSpan * 1.75f / Mathf.Max(1, flightCamera.pixelHeight));
        }
        private void OnDestroy()
        {
            if (visibility != null) visibility.SurveyTransitionCommitted -= Committed;
            if (owner != null && owner.Modes != null) owner.Modes.WorldModeChanged -= ModeChanged;
            if (origin != null) origin.Rebased -= Rebased;
            foreach (var slot in slots) if (slot != null) { if (slot.Root != null) Destroy(slot.Root); if (slot.Material != null) Destroy(slot.Material); }
            if (material != null) Destroy(material);
        }
    }
}
