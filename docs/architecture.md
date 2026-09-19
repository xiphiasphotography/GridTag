# Architecture

AGENTS.md is the source of truth. The solution targets net10.0, configured only
in Directory.Build.props. Core depends only on the .NET base class library.

Project references: Cli -> Vision -> Core, plus Cli -> Core. Vision is currently
an empty project; the CLI has only a placeholder entry point. No recognition,
image access, command handling, or Lua implementation is included in tasks 1-2.

Core provides normalized entry numbers, an immutable entry-list snapshot and
lookup, and precomputed confusable neighbours and proper substring hosts.
Analysis uses the fixed digit pairs in AGENTS.md, requires exactly one changed
digit, and preserves entry-list order. It does not make confidence decisions.

The later Lightroom plugin will exchange manifest/results JSON files with the
CLI, process Picks only, preserve manual overrides, and write only owned fields
through catalog SDK operations. Photos stay local; no RAW or XMP files are written.

Field generation, golden XMP comparisons, confidence matching, pipelines,
CLI commands, Lua integration, and evaluation remain subsequent tasks.
