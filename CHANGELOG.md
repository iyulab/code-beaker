# Changelog

All notable changes to this project are recorded here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

CodeBeaker is in its `0.x` line: the public contract is not frozen yet, and a
better design is adopted rather than deferred. Breaking changes therefore appear
in minor releases, and this file is where a consumer finds out about them.

## [0.3.0]

### Changed

- **All packages now target `net10.0`** (previously `net8.0`). .NET 8 leaves
  support in November 2026, and the framework-following dependencies
  (`Microsoft.Extensions.Hosting.Abstractions`, `Serilog.AspNetCore`) move to
  their 10.x lines with it. A consumer on `net8.0` or `net9.0` can no longer
  reference `CodeBeaker.Core`, `CodeBeaker.Commands`, or `CodeBeaker.Runtimes`
  without retargeting; nothing else in the public API changes for this reason.
- **The C# sandbox image moves to .NET 10** (`mcr.microsoft.com/dotnet/sdk:8.0`
  → `sdk:10.0`), and the C# entry of `language.list` / `GET /api/language` now
  reports `10.0` instead of `8.0`. This image is published for anyone to pull,
  so leaving it on .NET 8 past November 2026 would mean shipping an unpatched
  runtime. The image sets `DOTNET_ROLL_FORWARD=Major`, so user code targeting
  an older generation (`net8.0`, `net9.0`) still launches on the 10.0 runtime
  instead of failing to find its own — without that setting it does fail, which
  was verified in the built image. Two consequences are worth knowing: a
  sandbox runs with networking disabled unless the caller allows it, and only
  the SDK's own generation restores offline — a project pinned to `net8.0`
  needs network access to build, because its reference pack is not in the
  image. The sandbox account is unchanged (`coderunner`, UID 1000): the
  Ubuntu-based .NET 10 image ships a stock account at that UID, and the image
  now removes it so the UID stays the same across all four runtime images and
  matches the Helm `runAsUser`.
- **The Python, Node.js, and Go sandbox images move to currently supported
  generations** (`python:3.11-slim` → `3.13-slim`, `node:20-slim` → `24-slim`,
  `golang:1.21-alpine` → `1.27-alpine`), and their entries in `language.list` /
  `GET /api/language` now report `3.13`, `24`, and `1.27` instead of `3.11`, `20`,
  and `1.21`. Node 20 and Go 1.21 had both passed end of life, and these images are
  published for anyone to pull, so the reasoning that moved the C# image applies to
  them as well. Unlike .NET, none of the three offers a roll-forward switch: user
  code that depends on a standard-library module removed since Python 3.11, on a
  native add-on built against the Node 20 ABI, or on a Go toolchain older than the
  image now provides has to move with it. Each image was rebuilt and exercised with
  networking disabled to confirm that the sandbox account still holds UID 1000 and
  that user code still runs offline.
- **`StackExchange.Redis` moves to its 3.x line** (`2.8.16` → `3.1.31`).
  `CodeBeaker.Core` references it directly, so this is a dependency a consumer
  resolves rather than an internal detail. The 3.0 client rewrote its IO layer
  and speaks RESP3 by default, which requires a Redis server of version 6 or
  newer; the public API it exposes is unchanged. `RedisSessionStore` behaves
  the same — verified against a live Redis 7 server — but now calls the
  `Expiration`/`ValueCondition` overload of `SET` rather than the legacy
  `When`/`keepTtl` one, so its write and lock paths resolve to a single method.
- **The repository pins its SDK with `global.json`** (`10.0.100`, rolling
  forward to the latest feature band), so a local build no longer depends on
  whichever SDK happens to be installed.
- **JSON-RPC: `initialize` reports the version the server was actually built
  as.** `protocolVersion` was the constant `"0.2.0"` and had not been touched
  since the first commit, so it kept advertising the same value across two
  releases that changed the wire shape. It is now derived from the assembly
  version, which comes from the same `<Version>` the packages ship with.
- **JSON-RPC: `language.list` items now carry `displayName`, `version`,
  `aliases`, and `dockerImage` instead of `runtime`.** The old `runtime` value
  was the .NET class name of an internal metadata holder (`PythonRuntime`),
  which told a client nothing; the response is now the same shape the REST
  `GET /api/language` endpoint returns.

### Added

- **`LanguageCatalog` (`CodeBeaker.Runtimes`) and `LanguageDescriptor`
  (`CodeBeaker.Core.Models`)** — the single source of supported languages:
  canonical name, aliases, display name, sandbox image version, and Docker
  image. `LanguageCatalog.Resolve`/`TryResolve`/`IsSupported` accept a name or
  alias case-insensitively; `IsSupported` takes `string?` and answers `false`
  for `null`.
- **Aliases are honored everywhere, not only by the language endpoints.** A
  Docker session can now be created with `py`, `python3`, `js`, or `node`
  as well as the canonical names; previously the language endpoints advertised
  aliases the Docker runtime then rejected.

### Removed

- **`IRuntime`, `BaseRuntime`, `PythonRuntime`, `JavaScriptRuntime`,
  `GoRuntime`, `CSharpRuntime` (root `CodeBeaker.Runtimes` namespace), and
  `RuntimeRegistry`.** They held only a language name and a Docker image but
  were named as runtimes, so `CodeBeaker.Runtimes.PythonRuntime` collided with
  the execution runtime `CodeBeaker.Runtimes.Python.PythonRuntime` — a consumer
  writing `using CodeBeaker.Runtimes;` silently picked up the metadata class.
  Use `LanguageCatalog` for what they carried; nothing about execution changes,
  since `go` and `csharp` were always executed by `DockerRuntime` and
  `NativeProcessRuntime`, never by the removed types.
- **`Microsoft.AspNetCore.OpenApi` is no longer a dependency of the API
  server.** It was referenced but never called — the OpenAPI document and UI
  come from Swashbuckle — so it only added a package to resolve.
- **`docs/API.md`** — every HTTP path the server routes, in one place: the
  WebSocket endpoint, the language endpoints, the four health probes, and the
  metrics endpoint. It replaces the API reference the documentation site used to
  generate from a list written by hand, and is checked against the running
  application in both directions, so a path it names that the server does not
  serve — or a route it fails to mention — breaks the build.

### Fixed

- **The published API reference listed REST endpoints that do not exist.** It
  advertised `POST /api/execute` and `GET /api/execute/:id/status`, which the
  server has never served, and named the language endpoint `GET /api/languages`
  rather than `GET /api/Language`. The list was a constant in the documentation
  site's build script with nothing comparing it to the application. It is now
  `docs/API.md` (see Added) and the comparison is a test.
- **The Python sandbox image was advertised as 3.12; it is `python:3.11-slim`.**
  `GET /api/language`, `language.list`, and the architecture document now say
  3.11. The image itself is unchanged.
- **Docker resource usage is read through the supported stats API and no
  longer reports impossible CPU figures.** Usage was parsed by hand out of a
  raw stream, and a CPU counter that went backwards between two samples
  produced an enormous positive delta instead of zero, because the subtraction
  was unsigned. Counters the daemon omits now read as zero rather than
  throwing. `ResourceUsage.MemoryUsagePercent` and `CpuUsagePercent` are
  plain `double` values, as the type always declared.
- **`Command` models deserialize again on System.Text.Json 9 and later.**
  `Command.Type` was mapped to the JSON property `"type"`, the same name the
  polymorphic type discriminator uses; newer System.Text.Json rejects that
  model with "contains property 'type' that conflicts with an existing metadata
  property name", which surfaced as an internal error on every
  `session.execute`. The property is now excluded from serialization — the
  discriminator alone carries `"type"` on the wire, so the JSON shape a client
  sends and receives is unchanged. Serializing a command as its base type also
  no longer writes `"type"` twice.
- **The documented command types did not match the ones the server accepts.**
  `docs/USAGE.md` named the shell type `execute_shell` (it is `shell`), used
  `execute_code` (it is `execute`), `create_directory` (`create_dir`) and
  `sourcePath`/`destinationPath` on a copy (`source`/`destination`), and
  documented two commands that do not exist at all, `delete_file` and
  `list_directory`. `docs/ARCHITECTURE.md` listed seven command types, naming
  the same two non-existent ones and omitting five real ones, and left `Python`
  out of the `RuntimeType` enum it reproduces. Every command type named in the
  documentation now exists, and the architecture document carries the table of
  which runtimes accept which types — support has always varied by runtime, and
  only `shell` is accepted everywhere.

## [0.2.0]

### Removed

- **JSON-RPC: `containerId` is gone from `session/create` and `session/list`
  responses.** It always carried the same value as the environment identifier
  for Docker sessions and was empty for every other runtime, so the only thing
  it actually told a caller was whether the session ran in a container. Use
  `environmentId` for the identifier and the new `runtimeType` for that
  distinction.
- **`Session.ContainerId` and `SessionData.ContainerId`** for the same reason.
  `Session.EnvironmentId` is now documented as the contract: the identifier the
  runtime issues for the execution environment — the container id for Docker,
  a runtime-specific id elsewhere — and the key session reconnection uses.

### Added

- **JSON-RPC: `runtimeType`** on `session/create` and `session/list` responses.
  Neither response previously said which runtime backed a session.
- **`DockerCleanupService` now exposes its sweep interval and maximum container
  age**, with `DefaultCleanupInterval` (1 hour) and `DefaultMaxContainerAge`
  (24 hours) as the documented defaults.

### Changed

- **Docker sessions now get `NetworkMode: bridge` by default**, instead of
  always being created with `none`. The runtime advertised
  `SupportsNetworkAccess: true` while never honoring it, so the isolation was
  accidental rather than configured. Network access follows
  `SecurityConfig.SandboxDisableNetwork`, which defaults to `false` — a session
  created without an explicit security configuration reaches the network. Set
  `SandboxDisableNetwork = true` to keep a container off the network.
- **A session that exceeds its configured memory limit is now terminated.**
  The API host registers the resource monitor that enforces
  `SessionConfig.MemoryLimitMB`: it samples live sessions and closes one that
  goes over its hard limit. Docker sessions already carried that limit as a
  container-level constraint the daemon enforced, so the change is visible on
  the runtimes the kernel does not police — and the monitor closes the session
  rather than leaving a dead environment behind. A consumer that hosts the
  library itself gets the same behavior by registering
  `ResourceMonitoringService`, which nothing did before.
- **`DockerCleanupService` is a `BackgroundService`**, not a one-shot
  `IHostedService`, and its constructor takes `IDockerClient` rather than the
  sealed `DockerClient`.

### Fixed

- **The Bun runtime ran TypeScript as JavaScript.** It picked the temporary file's
  extension by looking for `import`/`export` in the source rather than at the requested
  language, so TypeScript that used neither was written as `.js` — and Bun, which decides
  how to parse a file from its extension, then rejected the type annotations it had just
  been asked to run. The extension now follows `ExecuteCodeCommand.Language`.
- **Command arguments containing spaces were split into several arguments.**
  Every process-spawning runtime joined its argument list into a single command
  line, which is then re-parsed on whitespace, so one argument holding a script
  or a path with spaces reached the child process as several. Through a POSIX
  shell the result was a silent wrong answer rather than an error: `sh -c
  <script>` ran only the fragment before the first space, produced no output and
  still reported success. Arguments are now passed as a list and escaped for the
  platform.
- **Docker containers were never reclaimed and sessions could not be
  reconnected.** The session's environment identifier was a synthetic GUID the
  daemon knew nothing about, so session close silently failed to stop or remove
  the container, and reconnection from another API instance always failed. The
  identifier is now the container's own id.
- **Cross-instance session reconnection is implemented**, including its failure
  paths: a session whose environment is gone is reconciled and its leftover
  container reclaimed, while an environment whose state cannot be determined
  raises rather than being reported as absent.
- **Zombie container cleanup runs on its interval**, not once at process start,
  and a container's age is measured as an absolute instant — it was parsed into
  the host's local time and subtracted from a UTC clock, shifting every age by
  the host's own UTC offset.
- **Shell injection in `ExecuteListFilesAsync`**: the path and pattern were
  concatenated into `sh -c` unquoted.
- Resource-usage history and violation handling, previously left unimplemented
  behind their public surface.
- Documentation links that pointed at files and directories not present in the
  repository, and a quick-start clone URL that 404'd.

### Security

- See the shell-injection fix above.

## [0.1.0]

First release published to nuget.org (`CodeBeaker.Core`, `CodeBeaker.Commands`,
`CodeBeaker.Runtimes`).
