# ADR-0004: V2 storage is a clean break

- Status: accepted

Architecture-deepening storage writes beneath version 2 profile and cache paths. Version 1 files are neither read nor migrated, and are never overwritten or deleted. A v2 profile therefore begins with fresh Survey progress while old evidence remains recoverable.

V2 cache documents use checksum verification and content-specific filenames.
Document bytes are flushed before the atomic index replacement. The active
manifest is committed after its overview document. Active base entries are pinned
against indexed LRU eviction. Accounting for quarantined/orphan bytes within the
physical 10 GiB ceiling still requires qualification and hardening.
