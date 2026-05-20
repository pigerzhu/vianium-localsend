# Vianium LocalSend — Smoke Tests

Automated smoke-test runner for the Vianium LocalSend stack, by
[Angel Careaga](https://github.com/AngelCareaga).

This Windows Phone 8.1 AppContainerExe deploys side-by-side with the
main `Vianium.LocalSend` app and runs a curated set of fast,
deterministic tests that pin the contracts every LocalSend peer relies
on.

## Coverage

| Area | Tests |
|---|---|
| JSON parser ([`SimpleJsonParser`](../../src/Vianium.LocalSend/Json/SimpleJsonParser.cs)) | primitives, objects + arrays, escaped strings, numbers, roundtrip |
| Crypto ([`TokenGenerator`](../../src/Vianium.LocalSend/Protocol/TokenGenerator.cs), [`FingerprintGenerator`](../../src/Vianium.LocalSend/Crypto/FingerprintGenerator.cs)) | entropy + format, GUID session id |
| Protocol constants ([`ProtocolConstants`](../../src/Vianium.LocalSend/Protocol/ProtocolConstants.cs)) | spec compliance pin |
| `DeviceInfo` DTO | multicast / info / register roundtrips, missing-field fallback |
| `FileDto` | roundtrip, mime-type guesser (16 cases) |
| HTTP request parser ([`RequestParser`](../../src/Vianium.LocalSend/Server/RequestParser.cs)) | GET, POST with body, multi-header, empty-payload rejection |
| End-to-end protocol flow | prepare-upload request + response shape, register payload |

Total: **22 smoke cases** across **8 test files**.

## How it works

The smoke-test project does not refactor the production code. Instead,
the `.csproj` **links the LocalSend source files at compile time** via
`<Compile Include="..\..\src\Vianium.LocalSend\...">` with `<Link>`
attributes. The smoke-test assembly therefore contains its own copy of
the production code under the same namespaces, which gives the tests
direct access to the `internal` API surface without changing visibility
on the shipping app.

## Running

Open the solution at the repo root:

```cmd
cd D:\path\to\vianium-localsend
start Vianium.LocalSend.sln
```

In Visual Studio 2015 (or 2013), select `Vianium.LocalSend.SmokeTests`
as the startup project, deploy + launch on the WP 8.1 emulator, and tap
**Run all tests**. Each result row turns green (`✓`) or red (`✗`) as
the test completes; the summary banner at the top reports the total
pass / fail count and the wall-clock duration.

The runner is dependency-free: no NUnit / xUnit / MSTest. It targets
exactly what ships on 512 MB-class hardware.

## Adding new tests

1. Add a new `Tests/<Component>Smoke.cs` with a `static class` exposing
   `public static Task<SmokeResult>` methods.
2. Register the new cases in `SmokeTestRunner.BuildCaseList()` with a
   human-readable name.
3. Include the new file in `Vianium.LocalSend.SmokeTests.csproj` under
   the existing smoke-tests `<ItemGroup>`.
4. Build + deploy. The MainPage list refreshes from the registry on the
   next **Run all tests**.

## License

PolyForm Noncommercial 1.0.0, same as the main app. See `LICENSE` at the
repo root.

## Author

**Angel Careaga** ·
[hello@angelcareaga.com](mailto:hello@angelcareaga.com) ·
[@AngelCareaga](https://github.com/AngelCareaga) ·
[angelcareaga.com](https://angelcareaga.com)
