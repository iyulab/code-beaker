# HTTP Surface

Every HTTP path the server routes, and what each one is for. The JSON-RPC command
protocol carried over the WebSocket connection is documented separately in the
[Usage Guide](USAGE.md) — this page covers the HTTP surface itself.

The table below is checked against the running application: a path listed here that
the server does not serve, or a route the server serves that is missing here, fails
the test suite. `ANY` means the route is not restricted to a single HTTP method.

| Method | Path | Purpose |
|---|---|---|
| `ANY` | `/ws/jsonrpc` | The WebSocket endpoint. A request that is not a WebSocket handshake is answered with `400`. Everything the sandbox does is driven through here. |
| `GET` | `/api/Language` | The languages this server supports, as the language catalog reports them — name, display name, version, aliases and sandbox image. |
| `GET` | `/api/Language/{name}` | One language, resolved by name or alias. `404` with a `LANGUAGE_NOT_FOUND` error when the name is not supported. |
| `ANY` | `/health` | Full health report, including every registered check. |
| `ANY` | `/health/live` | Liveness probe — answers as long as the process is running, without consulting any check. |
| `ANY` | `/health/ready` | Readiness probe — reports whether the server is ready to accept work. |
| `ANY` | `/health/startup` | Startup probe — reports whether initialization has finished. |
| `GET` | `/metrics` | Prometheus exposition of the server's metrics. Served by middleware rather than a route, so it does not appear in the OpenAPI document. |

## The interactive documentation

Swagger UI is mounted at `/swagger`, and the OpenAPI document at
`/swagger/v1/swagger.json`. Both are wired up in the `Development` environment only,
so neither is present in a production deployment.

The document describes the `/api/*` routes above. The WebSocket endpoint, the health
probes and the metrics endpoint are not part of it — they are not MVC actions, so
nothing generates OpenAPI entries for them.

## Where the port comes from

None of the paths above carry a host or a port, because that depends on how the
server is started: `docker compose up` publishes it on `5000`, while `dotnet run`
uses the profile in `launchSettings.json`. Substitute whichever applies.
