# estate/lib — binaries checked into source control

Every legacy estate has this folder. Vendor DLLs from before the team used NuGet,
or from vendors that never published a package, referenced by `<HintPath>`.
No source, no package metadata, often no licence file, and no upgrade path you
can see from here.

The files in this folder are **zero-byte placeholders** with invented names.
`legacy map` reports every `HintPath` reference that points into `lib/`,
because each one is a question for week one: *does a supported version of this
exist for modern .NET, and who owns finding out?*
