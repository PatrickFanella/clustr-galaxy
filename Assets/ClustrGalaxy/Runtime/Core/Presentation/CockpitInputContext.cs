using System.Collections.Generic;
namespace ClustrGalaxy.Core.Presentation
{
    // One read model for every cockpit input adapter. Leases survive hidden parent panels.
    public sealed class CockpitInputContext
    {
        private readonly HashSet<long> modalOwners = new();
        private long nextOwner;
        public bool CommandOpen { get; private set; }
        public bool ChartOpen { get; private set; }
        public bool CompatibilityModal { get; private set; }
        public bool Paused => CompatibilityModal || modalOwners.Count > 0;
        public bool PilotEnabled => !Paused && !CommandOpen && !ChartOpen;
        public bool NavigationEnabled => !Paused && !CommandOpen;
        public int ModalCount => modalOwners.Count;
        public void SetCommand(bool open) => CommandOpen = open;
        public void SetChart(bool open) => ChartOpen = open;
        public void SetCompatibilityModal(bool open) => CompatibilityModal = open;
        public long AcquireModal() { var id = ++nextOwner; modalOwners.Add(id); return id; }
        public bool ReleaseModal(long id) => modalOwners.Remove(id);
    }
}
