using System;
using System.IO;
using System.Linq;
using ClustrGalaxy.Core.Activities;
using ClustrGalaxy.Core.Domain;
using ClustrGalaxy.Core.Persistence;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ClustrGalaxy.Persistence
{
    public sealed class ActivityFileRepository : IActivityRepository
    {
        private readonly IDurableDocumentStore store;
        public ActivityFileRepository(IDurableDocumentStore store) => this.store = store ?? throw new ArgumentNullException(nameof(store));
        public static string PathFor(ActivityIdentity identity)
        {
            var survey = ProfilePaths.Survey(identity.ProfileId, identity.Snapshot.StorageNamespace,
                identity.Snapshot.RevisionId, identity.Snapshot.CatalogId);
            return Path.Combine(Path.GetDirectoryName(survey), Path.GetFileNameWithoutExtension(survey) +
                ".activities-" + identity.Mode.ToString().ToLowerInvariant() + ".json");
        }
        public ActivityState LoadOrCreate(ActivityIdentity identity)
        {
            if (!store.TryRead(PathFor(identity), out var json)) return new ActivityState(identity);
            try { return Parse(json, identity); }
            catch (Exception exception) { throw new InvalidDataException("Activity save is unavailable; original file preserved.", exception); }
        }
        public void Save(ActivityState state)
        {
            var root = new JObject
            {
                ["schema_version"] = ActivityState.SchemaVersion,
                ["profile_id"] = state.Identity.ProfileId,
                ["snapshot_namespace"] = state.Identity.Snapshot.StorageNamespace,
                ["revision_id"] = state.Identity.Snapshot.RevisionId,
                ["catalog_id"] = state.Identity.Snapshot.CatalogId,
                ["world_mode"] = state.Identity.Mode.ToString(),
                ["followed_trail"] = state.FollowedTrail,
                ["preferences"] = new JObject
                {
                    ["save_best_ghosts"] = state.Preferences.SaveBestGhosts,
                    ["automatic_tuning"] = state.Preferences.AutomaticTuning,
                    ["music_muted"] = state.Preferences.MusicMuted,
                    ["music_preset"] = state.Preferences.MusicPreset,
                    ["reduced_audio"] = state.Preferences.ReducedAudio,
                    ["reduced_motion"] = state.Preferences.ReducedMotion,
                    ["music_volume"] = state.Preferences.MusicVolume,
                    ["tempo"] = state.Preferences.Tempo,
                    ["celebration_intensity"] = state.Preferences.CelebrationIntensity
                },
                ["processed_intents"] = new JArray(state.ProcessedIntents),
                ["expedition"] = state.Expedition==null?JValue.CreateNull():(JToken)new JObject
                {["id"]=state.Expedition.Plan.Id,["mood"]=(int)state.Expedition.Plan.Mood,["stops"]=new JArray(state.Expedition.Plan.Stops.Select(x=>x.Value)),["current"]=state.Expedition.Current,["status"]=(int)state.Expedition.Status},
                ["displays"] = new JArray(state.Displays.Select(item=>new JObject{["slot"]=item.Slot,["kind"]=(int)item.Kind,["reference"]=item.Reference,["caption"]=item.Caption})),
                ["flights"] = new JArray(state.Flights.Select(record=>new JObject
                { ["course_id"]=record.CourseId,["version"]=record.Version,["mode"]=(int)record.Mode,
                    ["seconds"]=record.Seconds,["accuracy"]=record.Accuracy,["tuning_id"]=record.TuningId,["ghost"]=WriteGhost(record.Ghost) })),
                ["designs"] = new JArray(state.Designs.Select(design => new JObject
                {
                    ["personal_id"] = design.PersonalId, ["palette"] = design.Palette, ["music_preset"] = design.MusicPreset,
                    ["nodes"] = new JArray(design.Nodes.Select(node => new JObject { ["id"] = node.Id.Value, ["x"] = node.X, ["y"] = node.Y })),
                    ["edges"] = new JArray(design.Edges.Select(edge => new JObject { ["first"] = edge.First.Value, ["second"] = edge.Second.Value }))
                })),
                ["evidence_boards"] = new JArray(state.EvidenceBoards.Select(board => new JObject
                {
                    ["activity_id"] = board.ActivityId,
                    ["nodes"] = new JArray(board.Layout.Nodes.Select(node => new JObject { ["id"] = node.Id.Value, ["x"] = node.X, ["y"] = node.Y })),
                    ["edges"] = new JArray(board.Layout.Edges.Select(edge => new JObject { ["first"] = edge.First.Value, ["second"] = edge.Second.Value }))
                })),
                ["records"] = new JArray(state.Records.Select(record => new JObject
                {
                    ["id"] = record.Id, ["content_version"] = record.ContentVersion,
                    ["observations"] = new JArray(record.Observations), ["completed"] = record.Completed,
                    ["interpretation"] = record.Interpretation, ["explanation"] = record.Explanation
                })),
                ["diary"] = new JArray(state.Diary.Select(entry => new JObject
                { ["id"] = entry.Id, ["text"] = entry.Text, ["utc"] = entry.Utc.ToString("O"), ["retained"] = entry.Retained }))
            };
            store.Write(PathFor(state.Identity), root.ToString(Formatting.None), text =>
            { try { Parse(text, state.Identity); return true; } catch { return false; } });
        }
        private static ActivityState Parse(string json, ActivityIdentity expected)
        {
            if (json == null || json.Length > 8 * 1024 * 1024) throw new InvalidDataException("Activity document exceeds its limit.");
            using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None, MaxDepth = 16 };
            var root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (reader.Read()) throw new InvalidDataException("Trailing activity document content.");
            if (Integer(root, "schema_version") != ActivityState.SchemaVersion || Text(root, "profile_id") != expected.ProfileId ||
                Text(root, "snapshot_namespace") != expected.Snapshot.StorageNamespace || Integer(root, "revision_id") != expected.Snapshot.RevisionId ||
                Integer(root, "catalog_id") != expected.Snapshot.CatalogId || Text(root, "world_mode") != expected.Mode.ToString())
                throw new InvalidDataException("Activity identity or schema mismatch.");
            var records = Array(root, "records").Select(value =>
            {
                if (value["completed"]?.Type != JTokenType.Boolean) throw new InvalidDataException("Missing completion flag.");
                return new ActivityRecord(Text(value, "id"), checked((int)Integer(value, "content_version")),
                    Array(value, "observations").Select(String), (bool)value["completed"], Text(value, "interpretation"), Text(value, "explanation"));
            });
            var diary = Array(root, "diary").Select(value => new DiaryEntry(Text(value, "id"), Text(value, "text"),
                DateTime.ParseExact(Text(value, "utc"), "O", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind), value["retained"]==null?Text(value,"id").StartsWith("bookmark:",StringComparison.Ordinal):Boolean(value,"retained")));
            var prefs = root["preferences"];
            var preferences = prefs == null ? new ActivityPreferences() : new ActivityPreferences(
                Boolean(prefs, "automatic_tuning"), Boolean(prefs, "music_muted"), Boolean(prefs, "reduced_motion"),
                Number(prefs, "music_volume"), checked((int)Integer(prefs, "tempo")), Number(prefs, "celebration_intensity"),
                prefs["music_preset"] == null ? 0 : checked((int)Integer(prefs, "music_preset")),
                prefs["reduced_audio"] != null && Boolean(prefs, "reduced_audio"),
                prefs["save_best_ghosts"] != null && Boolean(prefs,"save_best_ghosts"));
            var designs = root["designs"] == null ? System.Array.Empty<ConstellationDesign>() : Array(root, "designs").Select(value =>
                new ConstellationDesign(Text(value, "personal_id"), Array(value, "nodes").Select(node =>
                    new DesignNode((GalaxyEntityId)Text(node, "id"), Number(node, "x"), Number(node, "y"))),
                    Array(value, "edges").Select(edge => new DesignEdge((GalaxyEntityId)Text(edge, "first"), (GalaxyEntityId)Text(edge, "second"))),
                    checked((int)Integer(value, "palette")), checked((int)Integer(value, "music_preset")))).ToArray();
            var flights = root["flights"]==null ? System.Array.Empty<FlightRecord>() : Array(root,"flights").Select(value=>
                new FlightRecord(Text(value,"course_id"),checked((int)Integer(value,"version")),
                    (ClustrGalaxy.Core.Navigation.NavigationFlightMode)checked((int)Integer(value,"mode")),
                    Double(value,"seconds"),Double(value,"accuracy"),Text(value,"tuning_id"),ReadGhost(value["ghost"]))).ToArray();
            ExpeditionRecord expedition=null;var trip=root["expedition"];
            if(trip!=null&&trip.Type!=JTokenType.Null)expedition=new ExpeditionRecord(new ExpeditionPlan(Text(trip,"id"),(ExpeditionMood)checked((int)Integer(trip,"mood")),Array(trip,"stops").Select(x=>(GalaxyEntityId)String(x))),checked((int)Integer(trip,"current")),(ExpeditionStatus)checked((int)Integer(trip,"status")));
            var displays=root["displays"]==null?System.Array.Empty<ObservatoryDisplay>():Array(root,"displays").Select(value=>new ObservatoryDisplay(checked((int)Integer(value,"slot")),(ObservatoryItemKind)checked((int)Integer(value,"kind")),Text(value,"reference"),value["caption"]==null?"":Text(value,"caption"))).ToArray();
            var boards = root["evidence_boards"] == null ? System.Array.Empty<EvidenceBoard>() : Array(root,"evidence_boards").Select(value =>
                new EvidenceBoard(Text(value,"activity_id"), new ConstellationDesign(Text(value,"activity_id"),
                    Array(value,"nodes").Select(node => new DesignNode((GalaxyEntityId)Text(node,"id"),Number(node,"x"),Number(node,"y"))),
                    Array(value,"edges").Select(edge => new DesignEdge((GalaxyEntityId)Text(edge,"first"),(GalaxyEntityId)Text(edge,"second")))))).ToArray();
            return new ActivityState(expected, records, Array(root, "processed_intents").Select(String), diary, Text(root, "followed_trail"), preferences, designs, flights, displays, expedition, boards);
        }
        private static JToken WriteGhost(FlightGhost ghost)
        {
            if(ghost==null)return JValue.CreateNull();
            using var buffer=new MemoryStream();ReplayPackage.Write(buffer,ghost.Replay);
            return new JObject{["x"]=ghost.Anchor.X,["y"]=ghost.Anchor.Y,["z"]=ghost.Anchor.Z,["replay"]=Convert.ToBase64String(buffer.ToArray())};
        }
        private static FlightGhost ReadGhost(JToken value)
        {
            if(value==null||value.Type==JTokenType.Null)return null;
            var encoded=Text(value,"replay");if(encoded.Length>640000)throw new InvalidDataException("Best ghost exceeds the bounded pose and annotation payload.");
            using var buffer=new MemoryStream(Convert.FromBase64String(encoded));
            return new FlightGhost(ReplayPackage.Read(buffer),new ReplayPosition(Double(value,"x"),Double(value,"y"),Double(value,"z")));
        }
        private static double Double(JToken root,string name)=>root[name]?.Type==JTokenType.Float||root[name]?.Type==JTokenType.Integer ? (double)root[name] : throw new InvalidDataException("Missing number: "+name);
        private static bool Boolean(JToken root, string name) => root[name]?.Type == JTokenType.Boolean ? (bool)root[name] : throw new InvalidDataException("Missing boolean: " + name);
        private static float Number(JToken root, string name) => root[name]?.Type == JTokenType.Float || root[name]?.Type == JTokenType.Integer ? (float)root[name] : throw new InvalidDataException("Missing number: " + name);
        private static JArray Array(JToken root, string name) => root[name] as JArray ?? throw new InvalidDataException("Missing array: " + name);
        private static string String(JToken value) => value?.Type == JTokenType.String ? (string)value : throw new InvalidDataException("Expected string.");
        private static string Text(JToken root, string name) => String(root[name]);
        private static long Integer(JToken root, string name) => root[name]?.Type == JTokenType.Integer ? (long)root[name] : throw new InvalidDataException("Missing integer: " + name);
    }
}
