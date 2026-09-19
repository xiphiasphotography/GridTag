# Open questions

## Input and documentation discrepancies

- The session sample and AGENTS.md section 6 session example omit schemaVersion,
  while section 3 requires it on every JSON exchange file. Resolve before task 3
  introduces EventContext; the sample has not been changed.
- The sample CSV puts car/class after the driver columns, whereas section 6 lists
  them before drivers. Header-based loading has been proposed to the owner.
- The original README allows lowering the target framework for older SDKs.
  AGENTS.md and the owner's explicit instruction require net10.0; no downgrade
  is allowed. SDK 10.0.401 is now installed.

## Lightroom SDK: still unverified

No Lightroom Classic instance was available for task 7. Retain these checks
from AGENTS.md for manual Lightroom testing; the checklist is in
`docs/lightroom-test-plan.md`:

- Multiple drivers in personShown: separator behavior (default proposal: comma + space).
- LrTasks.pcall as a yield-safe wrapper for LrTasks.execute.
- Windows command quoting, including paths with spaces.
- createCollection(name, nil, true) and collection addPhotos/removePhotos behavior.
- Loading when LrSdkVersion exceeds the running Lightroom SDK version.
- Write-gate chunking and review collections with hundreds of photos.
- `photo:setRawMetadata` support for accessibility and custom metadata fields
  on the owner's installed Lightroom SDK.
