using System;
using System.Collections.Generic;
using System.Linq;

namespace ClustrGalaxy.Constellations
{
    /// <summary>
    /// Player-local authoring state. This has no Unity, transport, or persistence side effects
    /// until a caller explicitly invokes SaveLocally or ConfirmLocalQueue.
    /// </summary>
    public sealed class PersonalConstellationDraft
    {
        public const int MinimumMembers = 3;
        public const int MaximumMembers = 12;
        public const string QueuedLocallyStatus = "QUEUED LOCALLY — SUBMISSION SERVICE UNAVAILABLE";

        private readonly Func<IEnumerable<string>> knownStableIds;
        private readonly List<PersonalConstellationDraftMember> members = new();
        private string savedPersonalId = string.Empty;

        public PersonalConstellationDraft(Func<IEnumerable<string>> knownStableIds)
        {
            this.knownStableIds = knownStableIds ?? throw new ArgumentNullException(nameof(knownStableIds));
        }

        public IReadOnlyList<PersonalConstellationDraftMember> Members => members;
        public string Name { get; private set; } = string.Empty;
        public string Reason { get; private set; } = string.Empty;
        public string Error { get; private set; } = string.Empty;
        public string Status { get; private set; } = string.Empty;
        public string SavedPersonalId => savedPersonalId;
        public bool AwaitingQueueConfirmation { get; private set; }
        public bool HasSavedConstellation => !string.IsNullOrWhiteSpace(savedPersonalId);

        public void SetName(string value)
        {
            var normalized = value ?? string.Empty;
            if (string.Equals(Name, normalized, StringComparison.Ordinal)) return;
            Name = normalized;
            InvalidateSavedConstellation();
        }

        public void SetReason(string value)
        {
            var normalized = value ?? string.Empty;
            if (string.Equals(Reason, normalized, StringComparison.Ordinal)) return;
            Reason = normalized;
            InvalidateSavedConstellation();
        }

        public bool TryAdd(string stableId, string displayLabel = null)
        {
            ClearMessage();
            if (string.IsNullOrWhiteSpace(stableId)) return Fail("Choose an identified Survey target first.");
            var normalized = stableId.Trim();
            var known = new HashSet<string>(knownStableIds() ?? Array.Empty<string>(), StringComparer.Ordinal);
            if (!known.Contains(normalized)) return Fail("Only identified Survey entities can join a constellation.");
            if (members.Any(member => string.Equals(member.StableId, normalized, StringComparison.Ordinal)))
                return Fail("That entity is already in the draft.");
            if (members.Count >= MaximumMembers) return Fail("A personal constellation can contain at most 12 entities.");
            members.Add(new PersonalConstellationDraftMember(normalized, displayLabel));
            InvalidateSavedConstellation();
            Status = $"ADDED {members.Count}/{MaximumMembers}";
            return true;
        }

        public bool Remove(string stableId)
        {
            var index = members.FindIndex(member => string.Equals(member.StableId, stableId, StringComparison.Ordinal));
            if (index < 0) return Fail("That entity is not in the draft.");
            members.RemoveAt(index);
            InvalidateSavedConstellation();
            Status = $"REMOVED {members.Count}/{MaximumMembers}";
            Error = string.Empty;
            return true;
        }

        public void Clear()
        {
            members.Clear();
            InvalidateSavedConstellation();
            Error = string.Empty;
            Status = "DRAFT CLEARED";
        }

        public bool TrySaveLocally(PersonalConstellationStore store)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            ClearMessage();
            if (!TryValidate()) return false;
            try
            {
                var saved = store.Save(Name, Reason, members.Select(member => member.StableId), knownStableIds());
                savedPersonalId = saved.id;
                AwaitingQueueConfirmation = false;
                Status = "SAVED LOCALLY";
                return true;
            }
            catch (ArgumentException exception)
            {
                return Fail(exception.Message);
            }
        }

        public bool RequestLocalQueueConfirmation()
        {
            ClearMessage();
            if (!HasSavedConstellation) return Fail("Save this constellation locally before queuing it for review.");
            AwaitingQueueConfirmation = true;
            Status = "CONFIRM LOCAL QUEUE — NO SUBMISSION SERVICE IS CONNECTED";
            return true;
        }

        public bool ConfirmLocalQueue(ConstellationSubmissionOutbox outbox)
        {
            if (outbox == null) throw new ArgumentNullException(nameof(outbox));
            ClearMessage();
            if (!AwaitingQueueConfirmation) return Fail("Choose QUEUE FOR REVIEW before confirming the local queue.");
            try
            {
                outbox.QueueForSubmission(savedPersonalId, confirmed: true);
                AwaitingQueueConfirmation = false;
                Status = QueuedLocallyStatus;
                return true;
            }
            catch (ArgumentException exception)
            {
                return Fail(exception.Message);
            }
        }

        public void CancelLocalQueueConfirmation()
        {
            AwaitingQueueConfirmation = false;
            Error = string.Empty;
            Status = "LOCAL QUEUE CANCELLED";
        }

        public bool TryValidate()
        {
            ClearMessage();
            try
            {
                PersonalConstellation.Create(default, Name, Reason, members.Select(member => member.StableId), knownStableIds(), "validation-only");
                return true;
            }
            catch (ArgumentException exception)
            {
                return Fail(exception.Message);
            }
        }

        private void InvalidateSavedConstellation()
        {
            if (!HasSavedConstellation && !AwaitingQueueConfirmation) return;
            savedPersonalId = string.Empty;
            AwaitingQueueConfirmation = false;
        }

        private void ClearMessage()
        {
            Error = string.Empty;
            Status = string.Empty;
        }

        private bool Fail(string message)
        {
            Error = message;
            Status = string.Empty;
            return false;
        }
    }

    public readonly struct PersonalConstellationDraftMember
    {
        public PersonalConstellationDraftMember(string stableId, string displayLabel)
        {
            StableId = stableId ?? string.Empty;
            DisplayLabel = string.IsNullOrWhiteSpace(displayLabel) ? StableId : displayLabel.Trim();
        }

        public string StableId { get; }
        public string DisplayLabel { get; }
    }
}
