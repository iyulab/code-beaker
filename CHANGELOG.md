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
- **The repository pins its SDK with `global.json`** (`10.0.100`, rolling
  forward to the latest feature band), so a local build no longer depends on
  whichever SDK happens to be installed.

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

### Fixed

- **The Python sandbox image was advertised as 3.12; it is `python:3.11-slim`.**
  `GET /api/language`, `language.list`, and the architecture document now say
  3.11. The image itself is unchanged.

- **`Command` models deserialize again on System.Text.Json 9 and later.**
  `Command.Type` was mapped to the JSON property `"type"`, the same name the
  polymorphic type discriminator uses; newer System.Text.Json rejects that
  model with "contains property 'type' that conflicts with an existing metadata
  property name", which surfaced as an internal error on every
  `session.execute`. The property is now excluded from serialization — the
  discriminator alone carries `"type"` on the wire, so the JSON shape a client
  sends and receives is unchanged. Serializing a command as its base type also
  no longer writes `"type"` twice.
- **`docs/USAGE.md` named the shell command type `execute_shell`.** The
  discriminator has always been `shell`; the examples now match what the
  server accepts.

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
