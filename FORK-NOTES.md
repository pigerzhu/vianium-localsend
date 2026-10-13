# pigerzhu fork: receiving and storage fixes

Changes maintained by pigerzhu. Original authorship and PolyForm Noncommercial license remain intact.

- Settings now offers a receive-folder picker using the W10M asynchronous picker API. Select the exact destination folder (for example Public/LocalSend, if exposed by the system picker). The app retains the broker-issued folder permission through FutureAccessList; both new ordinary-file receives and sandbox migration use it. Cancel leaves settings unchanged. This does not silently grant SYSTEM or bypass the picker. If Public is unavailable in the picker, another visible folder can be chosen; exact Public access via a SYSTEM helper remains outside this patch.
- Uploads decode HTTP chunked framing, chunk extensions and bounded trailers. A fixed-length or metadata-length upload must match its announced size; truncated, overlong or malformed transfers are failed and partial files are deleted. Real empty files remain supported.
- Media-library destinations remain separate. Existing app identity, publisher and version are retained so the fork does not pretend to be a signed upstream release.

Validation: XML parsing and diff whitespace checks passed; the actual ParseChunkSize method compiled with .NET Framework C# compiler and passed extension, zero, large size, malformed and overflow cases. Chunk-framing smoke test added (extensions, trailers, invalid sizes). W10M UWP ARM build and phone tests are required. The current workstation has no compatible WP8.1 MSBuild/SDK; no ARM package is claimed as built or tested.

Phone checks: choose a visible receive folder, restart and check retained access; migrate a small existing sandbox file; receive a nonempty text file, then a large APPX from Android; compare size/hash; test cancellation and truncated upload; confirm original media destinations. Never remove the original app before backing up its LocalState.

## GitHub Actions ARM build

Use Actions → Build WP8.1 ARM. `hosted` checks whether the GitHub Windows image has the required old toolchain; it fails explicitly if absent. `self-hosted` builds on a Windows x64 machine with VS2015, Windows Phone 8.1 tools and runner label `wp81`. Only manually dispatched builds run on that machine; pull requests do not execute there.

The workflow runs the original WP8.1 project and uploads APPX files/build logs. Packages are unsigned: signing/deployment must be handled separately, and no publisher private key is committed. This workflow does not install VS2015, invent a working hosted SDK, or claim a passing ARM build before it has run.

## W10M migration
The active project now targets UWP, minimum 14393, SDK 19041. The hosted Build W10M ARM workflow uses an IL Debug ARM package without .NET Native compilation. Folder and file pickers use async UWP APIs. WP8.1 support is no longer a target of this fork. Phone installation and persistence still need verification. Original publisher identity is retained for compatibility; fork display publisher is pigerzhu.

