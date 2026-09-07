# Changelog

All notable changes to this project are recorded here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

CodeBeaker is in its `0.x` line: the public contract is not frozen yet, and a
better design is adopted rather than deferred. Breaking changes therefore appear
in minor releases, and this file is where a consumer finds out about them.

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
