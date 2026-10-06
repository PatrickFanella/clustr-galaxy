using System;
using System.Collections.Generic;
using System.Linq;
using ClustrGalaxy.Core.Domain;

namespace ClustrGalaxy.Core.Activities
{
    public readonly struct DesignNode
    {
        public DesignNode(GalaxyEntityId id, float x, float y)
        {
            if (id.IsEmpty || float.IsNaN(x) || float.IsNaN(y) || float.IsInfinity(x) || float.IsInfinity(y) || Math.Abs(x) > 1 || Math.Abs(y) > 1)
                throw new ArgumentException("Design coordinates must be finite and normalized.");
            Id = id; X = x; Y = y;
        }
        public GalaxyEntityId Id { get; }
        public float X { get; }
        public float Y { get; }
    }
    public readonly struct DesignEdge
    {
        public DesignEdge(GalaxyEntityId first, GalaxyEntityId second)
        {
            if (first.IsEmpty || second.IsEmpty || first == second) throw new ArgumentException("Choose two different members.");
            if (first.CompareTo(second) < 0) { First = first; Second = second; }
            else { First = second; Second = first; }
        }
        public GalaxyEntityId First { get; }
        public GalaxyEntityId Second { get; }
    }
    public sealed class ConstellationDesign
    {
        public ConstellationDesign(string personalId, IEnumerable<DesignNode> nodes, IEnumerable<DesignEdge> edges = null,
            int palette = 0, int musicPreset = 0)
        {
            if (string.IsNullOrWhiteSpace(personalId) || palette < 0 || palette >= 6 || musicPreset < 0 || musicPreset > 2)
                throw new ArgumentException("Invalid constellation identity or appearance.");
            var points = (nodes ?? throw new ArgumentNullException(nameof(nodes))).OrderBy(n => n.Id).ToArray();
            var links = (edges ?? Array.Empty<DesignEdge>()).ToArray();
            if (points.Length < 3 || points.Length > 12 || points.Select(n => n.Id).Distinct().Count() != points.Length)
                throw new ArgumentException("A design requires 3–12 unique members.");
            foreach (var node in points) _ = new DesignNode(node.Id, node.X, node.Y);
            var ids = new HashSet<GalaxyEntityId>(points.Select(n => n.Id));
            var seen = new HashSet<(GalaxyEntityId,GalaxyEntityId)>();
            foreach (var edge in links)
                if (!ids.Contains(edge.First) || !ids.Contains(edge.Second) || edge.First.CompareTo(edge.Second) >= 0 || !seen.Add((edge.First,edge.Second)))
                    throw new ArgumentException("Personal edges must be unique connections between members.");
            PersonalId = personalId; Nodes = Array.AsReadOnly(points); Edges = Array.AsReadOnly(links); Palette = palette; MusicPreset = musicPreset;
        }
        public string PersonalId { get; }
        public IReadOnlyList<DesignNode> Nodes { get; }
        public IReadOnlyList<DesignEdge> Edges { get; }
        public int Palette { get; }
        public int MusicPreset { get; }
        public ConstellationDesign Move(GalaxyEntityId id, float x, float y)
        {
            if (!Nodes.Any(n => n.Id == id)) throw new ArgumentException("Unknown design member.");
            return new ConstellationDesign(PersonalId, Nodes.Select(n => n.Id == id ? new DesignNode(id, x, y) : n), Edges, Palette, MusicPreset);
        }
        public ConstellationDesign Connect(GalaxyEntityId first, GalaxyEntityId second)
        {
            var edge = new DesignEdge(first,second); var links = Edges.ToList();
            var index = links.FindIndex(e => e.First == edge.First && e.Second == edge.Second);
            if (index < 0) links.Add(edge); else links.RemoveAt(index);
            return new ConstellationDesign(PersonalId, Nodes, links, Palette, MusicPreset);
        }
        public ConstellationDesign Appearance(int palette, int preset) => new(PersonalId, Nodes, Edges, palette, preset);
        public static ConstellationDesign Default(string personalId, IEnumerable<GalaxyEntityId> members)
        {
            var ids = members.OrderBy(id => id).ToArray();
            return new ConstellationDesign(personalId, ids.Select((id,i) => new DesignNode(id,
                (float)Math.Sin(i * Math.PI * 2 / ids.Length) * .7f, (float)Math.Cos(i * Math.PI * 2 / ids.Length) * .7f)));
        }
    }
}
