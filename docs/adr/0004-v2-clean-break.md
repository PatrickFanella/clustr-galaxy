# ADR-0004: V2 storage is a clean break

- Status: accepted

Architecture-deepening storage writes beneath version 2 profile and cache paths. Version 1 files are neither read nor migrated, and are never overwritten or deleted. A v2 profile therefore begins with fresh Survey progress while old evidence remains recoverable.
