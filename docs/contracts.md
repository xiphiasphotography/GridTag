# Contracts

AGENTS.md sections 3 and 6 define the authoritative exchange contracts.
No JSON runtime contracts are implemented in tasks 1-2.

The entry list is semicolon-delimited UTF-8 with BOM. Required columns are
number, team, car, class, driver_1, driver_1_nat, driver_2, driver_2_nat.
Additional driver_N / driver_N_nat pairs represent additional drivers.
Names retain Unicode. Numbers are trimmed, have a leading # removed, are
uppercased invariantly, and lose leading zeros while retaining a single zero.
Duplicate normalized numbers are invalid.

Manifest and results samples declare schemaVersion 1 and use UTF-8 without BOM.
The session sample currently has no schemaVersion; this conflicts with the
versioning requirement and is recorded in open-questions.md. Samples are retained
unchanged pending clarification, rather than silently changing the contract.
