# Building safe code execution environments for AI coding agents

**AI coding agents need far more than simple script runners—they require full-featured development environments with file operations, terminal access, package managers, and debugging tools, all delivered through structured APIs while maintaining strong security isolation.** Research from production systems (SWE-agent, Devin, Replit) shows that **custom command interfaces outperform raw shell access by up to 20%**, and that **gVisor or Firecracker sandboxing** provides the optimal balance between security and functionality for multi-tenant scenarios. The evidence converges on a hybrid architecture: stateless REST for simple operations, WebSocket for real-time streaming, JSON-RPC for structured communication, with Docker/gVisor isolation and comprehensive resource limits.

This report synthesizes findings from commercial platforms (Claude Code, Copilot Workspace, Replit), open-source projects (SWE-agent, OpenHands, Aider), code execution engines (Judge0, Piston), communication protocols (LSP, DAP, Jupyter), and AI coding benchmarks (SWE-bench) to provide practical architectural guidance for Code-Beaker. The analysis reveals that successful systems share common patterns: **multi-channel communication separating control and data flows**, **capabilities-based negotiation** for flexible feature support, **language-agnostic abstractions** for broad compatibility, and **defense-in-depth security** layering multiple isolation mechanisms.

The central tension in designing these systems lies between security and functionality. AI agents need substantial capabilities—file system access, terminal commands, network requests, package installation—yet must operate safely in multi-tenant environments without compromising host systems or leaking data between users. This report provides a decision framework for navigating these trade-offs based on threat models, performance requirements, and operational constraints.

## Core capabilities AI coding agents require

Modern AI coding agents expect **full development environment access**, not just isolated code execution. Analysis of SWE-bench tasks reveals agents must handle repositories averaging **107.4 lines of code changes across 4.1 files**, requiring coordinated multi-file editing, dependency management, and iterative testing. The benchmark data shows 72% of successful Devin completions took over 10 minutes with multiple iterations, highlighting that **agents need feedback loops** where they execute code, observe results, identify errors, and refine solutions.

**File system operations** form the foundation, but naive implementations fail. SWE-agent research demonstrates that showing agents complete file contents overwhelms them—**chunked viewing with 100-line windows significantly improves performance**. Agents need read/write/delete operations with **explicit confirmation feedback** ("file saved successfully" vs empty responses), directory traversal with efficient search, and multi-file coordination capabilities. The file system must be isolated per user/session to prevent data leakage.

**Terminal and shell access** represents the most critical capability and biggest security challenge. Raw bash shells provide maximum flexibility but create security nightmares and confuse language models. SWE-agent's breakthrough insight: **custom command wrappers optimized for LLM comprehension** (like structured `edit`, `open`, `search` commands) achieve 20% better performance than raw terminal access. The execution environment must capture stdout/stderr separately, track exit codes, handle both synchronous quick commands and asynchronous long-running processes, and provide structured error reporting.

**Package manager integration** enables agents to install dependencies dynamically. Systems must support **npm, pip, cargo, maven, gradle** and other ecosystem tools, with both pre-cached common packages for speed and runtime installation for flexibility. Agents working with Python need virtual environments (venv/conda), Node.js agents need npm/yarn/pnpm, and multi-language projects require coordinated toolchain management. E2B demonstrates the pattern: `install_python_packages('pandas')` and `install_system_packages('gcc')` as structured API calls rather than raw shell commands.

**Build tool execution** and **test running** complete the development workflow. Agents must invoke webpack, vite, gradle, make, cargo, and parse their outputs to understand compilation errors and test failures. The SWE-agent experience shows **51.7% of agent-generated edits had linting errors**, making integrated linting with immediate feedback essential for iteration speed. Build systems should support incremental compilation, caching of artifacts, and structured error parsing.

**Debugging capabilities** increasingly differentiate advanced systems. Microsoft's debug-gym research proves that **interactive debugging with proper tools significantly outperforms basic stack trace analysis**. Agents need programmatic access to debuggers (pdb, lldb, gdb), ability to set breakpoints, inspect variables at runtime, and step through code execution. This requires exposing debugger functionality through structured APIs that LLMs can reliably invoke.

**Process management** underpins everything else. Agents must start processes, monitor resource usage (CPU, memory), control execution (pause/resume/terminate), run background services (web servers, databases), and manage process lifecycles safely. Resource limits—**memory, CPU time, process count, disk I/O**—prevent agents from accidentally or maliciously exhausting system resources. The --pids-limit flag (e.g., 100-200 processes) effectively prevents fork bombs.

**Port forwarding** enables web development workflows where agents create web applications that need testing. VS Code and Replit demonstrate the pattern: automatically detect services binding to ports, forward them through secure tunnels, generate stable authenticated URLs, and handle SSL/TLS termination. This allows human-in-the-loop review of generated interfaces and enables agents to test their own HTTP APIs.

**Git operations** provide version control and safety nets. Agents clone repositories, create branches, commit changes with generated messages, and create pull requests. Aider's git-native design automatically commits each logical changeset, making it trivial to review and roll back changes. However, **SWE-agent removes git remotes** to prevent information leakage where agents could pull test solutions—a critical security consideration for benchmark integrity.

**Multi-terminal session management** emerges as a requirement for sophisticated workflows. The agent-agnostic middleware vision describes agents dynamically spinning up "unlimited replicas of workspace, each sandbox branching off as AI explores possibilities like decision tree of timelines." E2B achieves ~150ms sandbox startup time, enabling rapid parallel experimentation that was previously impractical.

## Communication protocols and interface patterns

The protocol layer determines how cleanly AI applications can interact with execution environments. Research across multiple mature systems reveals **JSON-RPC as the dominant choice** for structured agent-to-environment communication, used successfully in LSP, DAP, Eclipse Theia, and implemented in modern code execution APIs.

**REST APIs** excel for stateless, request-response operations. Judge0 and Piston demonstrate the pattern: POST code for execution receiving a submission token, GET status checks with that token, and metadata endpoints for listing available languages. This stateless model simplifies horizontal scaling—workers can process any submission without session affinity. Judge0 processes thousands of submissions daily using this architecture with Redis queues mediating between API servers and worker pools.

**WebSocket connections** handle real-time requirements that REST cannot. Piston's WebSocket API showcases the bidirectional streaming pattern: clients send `init` messages, servers respond with `runtime` details, execution produces streaming `data` messages for stdout/stderr, and clients can inject `signal` commands (SIGKILL) mid-execution. This enables interactive coding scenarios where agents need to respond to program output in real-time or provide stdin input dynamically.

**Multi-channel architectures** separate control flow from data flow. Jupyter's kernel protocol uses five distinct ZeroMQ sockets: **Shell** for request/reply, **IOPub** for broadcast output, **stdin** for input requests, **Control** for high-priority commands, and **Heartbeat** for keepalive. This separation prevents slow operations from blocking control commands and enables efficient fan-out of output to multiple clients. The pattern applies broadly: execution engines should separate command submission, output streaming, and status monitoring into distinct channels.

**Capabilities-based negotiation** provides flexibility for evolving features. LSP pioneered this pattern with bidirectional capability exchange during initialization: clients declare what UI features they support, servers declare what language features they provide, and both gracefully handle optional capabilities. This means new LSP features deploy without breaking existing clients, and lightweight clients can omit expensive features. Code-Beaker should adopt similar initialization handshakes where clients and servers negotiate supported operations.

**Message framing** requires careful design. Three proven approaches emerge: **HTTP-style headers** (Content-Length: N followed by JSON body) used by DAP and optionally by LSP, **length-prefixed messages** in Jupyter over ZeroMQ, and **native WebSocket framing**. HTTP-style headers provide the best balance—simple to implement, language-agnostic, works over any byte stream (TCP, Unix sockets, pipes), and familiar to developers.

**Object references** solve the problem of complex nested data structures. DAP demonstrates this pattern for debugger state: threads return IDs, stackTrace requests use thread IDs to return frame IDs, scopes requests use frame IDs to return scope IDs, and finally variables requests use scope IDs to fetch actual values. This waterfall of references keeps messages compact while allowing deep exploration of state. References typically remain valid only during stable states (e.g., while debugger is paused).

**Streaming output patterns** range from simple to sophisticated. Judge0 uses **polling** (submit, then repeatedly GET status until complete), optionally supporting **long-polling** with wait=true. Piston adds **WebSocket streaming** for real-time stdout/stderr. Jupyter uses **publish-subscribe** where the kernel broadcasts output messages on IOPub and all connected clients receive them. For Code-Beaker, supporting both REST polling (simple clients) and WebSocket streaming (rich clients) provides the widest compatibility.

**State management** divides implementations philosophically. Judge0 and Piston REST APIs are stateless—each submission is independent, tracked only by token. Jupyter kernels are stateful—execution counter increments, variables persist, and state clears only on restart. LSP takes a hybrid approach—servers cache analyzed code for performance but re-sync on document changes. Code-Beaker should support both models: stateless execution for one-off runs, optional sessions for REPL-like interactions where variables persist across requests.

**Authentication and authorization** mechanisms vary by deployment model. Judge0 uses **X-Auth-Token and X-Auth-User headers** for API authentication. Jupyter uses **HMAC signatures** on messages with a shared secret from connection files. VS Code Remote relies on **SSH authentication** with the server running as the authenticated user. GitHub Codespaces uses **OAuth tokens** with automatic expiry and scope-based permissions. For multi-tenant deployments, Code-Beaker needs API key authentication at minimum, with optional OAuth integration for enterprise scenarios.

## Language Server Protocol patterns worth adopting

LSP provides an exceptionally clean separation between language-agnostic clients and language-specific servers, worth studying for Code-Beaker's design. The protocol achieves **complete UI and logic separation**—one language server can serve multiple editor clients, and editors gain support for new languages simply by connecting to new servers.

The **document lifecycle model** elegantly handles synchronization. When a document opens (`textDocument/didOpen`), the client sends complete content and the server caches it. Subsequent changes (`textDocument/didChange`) transmit only deltas or full content depending on negotiated sync mode. The server maintains the **source of truth in memory**, not filesystem, enabling support for unsaved changes. This pattern applies to Code-Beaker when agents work with multi-file projects—maintain working state in sandbox memory with explicit save operations.

**URI-based addressing** provides language-neutral resource identification. LSP uses URIs (file://, http://, custom schemes) rather than file paths, enabling remote files, in-memory documents, and virtual resources. Code-Beaker sandboxes could expose files via URIs, abstracting whether they're stored in container filesystems, S3, or databases.

**Position-based** rather than **AST-based** data types keep the protocol simple. LSP identifies code locations with line/character positions, not abstract syntax tree nodes. This requires no language-specific parsing in the protocol layer and remains stable across parse tree changes. When Code-Beaker reports execution errors, using line/column positions rather than AST nodes maintains language neutrality.

**Dynamic registration** of capabilities allows servers to add features after initialization. A server might initially not support formatting but register the capability after loading formatter configuration. This progressive feature disclosure pattern would benefit Code-Beaker—sandboxes might start minimally configured then register additional tools (debuggers, linters) as needed.

**Custom extensions** through experimental capability namespaces enable innovation without protocol changes. Language servers can expose proprietary features under `experimental.serverName.*` while remaining standards-compliant. Code-Beaker could support vendor-specific extensions this way without fragmenting core protocol compatibility.

## Jupyter kernel protocol insights for execution environments

Jupyter's architecture for interactive code execution offers direct parallels to AI agent requirements. The protocol manages **asynchronous execution with streaming output**—exactly what agents need when running builds, tests, or long computations. Jupyter's solutions are battle-tested across millions of notebooks.

The **five-socket architecture** might seem overengineered but solves real problems. Separating **Control** (shutdown, interrupt) from **Shell** (execution requests) ensures high-priority commands never queue behind long-running computations. The **IOPub** broadcast channel lets multiple clients observe the same kernel's output without polling. The **stdin** channel enables kernels to prompt for input during execution. Code-Beaker doesn't need five separate channels, but should separate **control flow** (starting/stopping sandboxes) from **data flow** (streaming output) from **status updates** (progress, resource usage).

**Message parent linking** creates execution context chains. Each message includes a `parent_header` referencing the request that triggered it. This allows clients to associate scattered output messages (stdout, stderr, display data) with specific execution requests—crucial when agents submit multiple commands and need to attribute results correctly. Code-Beaker should include request IDs in all responses and events.

**MIME bundles** for rich output enable language-agnostic data display. Instead of returning only plain text, Jupyter kernels can return dictionaries of MIME types: `text/plain`, `text/html`, `image/png`, `application/json`. Clients choose the richest format they support. This pattern would allow Code-Beaker to support simple text clients while enabling rich clients to display visualizations, tables, and formatted outputs.

**Execution counters** provide temporal ordering and traceability. Jupyter's In[n]/Out[n] numbering helps users understand execution order in non-linear notebooks. For AI agents, execution counters enable **audit trails** showing which commands ran when, essential for debugging agent behavior and providing human oversight.

**Kernel metadata** (`kernel_info_reply`) describes capabilities upfront: language name/version, supported operations, help resources. Code-Beaker sandboxes should similarly advertise their configuration: installed languages, available tools, resource limits, and enabled features. This allows agents to adapt their strategies based on environment capabilities.

## Debug Adapter Protocol relevance for agent workflows

DAP's design for language-agnostic debugging maps directly to agent debugging needs discovered in Microsoft's debug-gym research. Providing **structured debugging APIs** rather than raw debugger CLIs dramatically improves agent success rates.

The **initialization handshake** establishes mutual capabilities. Clients declare support for features like `supportsConfigurationDoneRequest`, adapters respond with capabilities like `supportsConditionalBreakpoints` and `supportsStepBack`. This negotiation prevents runtime errors from unsupported operations and enables progressive enhancement. Code-Beaker should adopt similar patterns: sandboxes declare which debuggers are available, agents declare which debugging features they want to use.

**Lifecycle management** through explicit states prevents race conditions. The sequence—Initialize → Launch/Attach → `initialized` event → Configuration (breakpoints) → `configurationDone` request → Execution—ensures deterministic startup. Code-Beaker's sandbox lifecycle should follow similar patterns with explicit state transitions and ready signals.

**Hierarchical object exploration** through references solves the infinite nesting problem. Threads have IDs, stackTraces reference thread IDs, scopes reference frame IDs, variables reference scope IDs. Each level fetches lazily on demand. This keeps messages small while enabling arbitrary depth exploration. If Code-Beaker agents need to inspect complex runtime state (large data structures, object graphs), lazy fetching via reference IDs is essential.

**Stopped events** with structured reasons (`breakpoint`, `exception`, `step`, `pause`) give clients semantic information about execution pauses. Agents need similar structured notifications: did the process exit normally, timeout, crash, get killed? Enumerating reason codes makes agent logic robust compared to parsing free-text messages.

**Standardized error codes** and success responses provide predictable behavior. DAP defines when requests return success (with optional body) versus error (with message and optional show user flag). Code-Beaker should define its own error taxonomy: `TIMEOUT`, `RESOURCE_LIMIT`, `PERMISSION_DENIED`, `INVALID_COMMAND`, etc., with both machine-readable codes and human-readable messages.

## Security and sandboxing: balancing isolation with functionality

Security architecture determines whether Code-Beaker can safely serve untrusted agents in multi-tenant environments. The research reveals **defense-in-depth** as the only viable approach—single security layers inevitably fail, but multiple overlapping protections provide robustness.

### Container-based isolation with hardening

**Docker containers** provide the baseline isolation most systems use. Containers leverage Linux namespaces (PID, network, mount, UTS, IPC, user) for process isolation and cgroups for resource limiting. However, **default Docker configurations are dangerously permissive** for untrusted code. Container escape vulnerabilities exist because containers **share the host kernel**—any kernel vulnerability potentially affects all containers.

**Hardening transforms Docker from moderate to high security.** Start by **running as non-root** with user namespace remapping, preventing privilege escalation even if an exploit exists. **Drop all capabilities** (`--cap-drop=ALL`) and add back only those strictly required (often zero for code execution). Apply **custom seccomp profiles** that whitelist only necessary syscalls rather than using the default blacklist approach. Enable **read-only root filesystem** (`--read-only`) with tmpfs for /tmp. Set **comprehensive resource limits**: memory (`--memory=512m`), CPU (`--cpus=1.0`), and critically **PID limit** (`--pids-limit=100`) which prevents fork bombs—the most common attack vector in code execution platforms.

A minimal secure Docker configuration for Python code execution:

```
docker run --rm --network=none --memory=256m --memory-swap=256m --cpus=0.5 
--pids-limit=50 --cap-drop=ALL --read-only --tmpfs /tmp:size=50m 
--security-opt=no-new-privileges -u 1000:1000 python:3.11-slim 
timeout 30s python /code/user_code.py
```

This configuration disables networking, limits memory and CPU, prevents process bombs, removes all privileges, uses read-only filesystem, runs as unprivileged user, and enforces execution timeout. Most AI coding workloads run fine within these constraints while gaining strong security boundaries.

### gVisor for production-grade isolation

**Google's gVisor** implements ~200 Linux syscalls in user-space, providing **VM-like isolation with container-like efficiency**. Application syscalls never reach the host kernel directly—gVisor's Sentry process intercepts them and handles them in a memory-safe Go implementation. This reduces attack surface from thousands of syscalls to approximately 200, dramatically limiting kernel exploitation opportunities.

**gVisor runs production workloads at Google scale**—App Engine, Cloud Functions, and Cloud Run use gVisor for multi-tenant isolation. The performance overhead ranges from 10-30% depending on workload, with I/O-intensive operations experiencing more impact due to the Gofer filesystem proxy. Recent optimizations like rootfs overlay have significantly improved filesystem performance.

For Code-Beaker, **gVisor provides the optimal balance for server-side untrusted code execution**. It's more secure than hardened Docker (application never touches host kernel) but far lighter than full VMs. Integration is straightforward: install runsc runtime and specify `--runtime=runsc` in Docker commands. For Kubernetes deployments, use GKE Sandbox or configure a RuntimeClass with gVisor.

The key trade-off: **syscall compatibility**. Not all Linux syscalls are implemented in gVisor, potentially causing issues with applications using exotic kernel features. However, standard development tools (compilers, interpreters, package managers) work reliably. Code-Beaker should test supported language runtimes against gVisor before committing to it.

### Firecracker microVMs for maximum isolation

**AWS Firecracker** delivers hardware-virtualized isolation with minimal overhead. Each microVM has its own kernel, providing the strongest security boundary while starting in ~125ms with ~5MB memory overhead. Firecracker powers AWS Lambda and Fargate at massive scale—millions of function invocations daily.

**Hardware virtualization** through KVM means guest code is fundamentally separated from the host. Even kernel exploits in guest VMs don't escape to the host. Firecracker's minimalist design—no BIOS, minimal device model, Rust implementation—reduces attack surface compared to traditional hypervisors like QEMU.

The **performance characteristics** make Firecracker viable for interactive workloads: 125ms startup, near-native CPU performance, fine-grained rate limiting for network and storage I/O, support for thousands of concurrent microVMs per host. This enables the parallel sandbox experimentation pattern where agents spin up multiple environments to test different approaches simultaneously.

**Firecracker requires KVM support** (Linux 4.14+ with virtualization enabled) and each microVM needs its own kernel and root filesystem. For Code-Beaker, this means more infrastructure complexity than containers—need to maintain kernel images and root filesystem templates. The benefits justify the complexity when security requirements are paramount, such as public-facing code execution services or enterprise deployments with strict isolation requirements.

### WebAssembly for client-side execution

**WebAssembly** offers unique security properties: **sandboxed by design** with memory safety enforced at compilation and runtime, no direct system access without explicit imports, and capability-based security model. WASM executes in a stack-based virtual machine with bounds-checked linear memory, making it impossible to escape the sandbox without vulnerabilities in the WASM runtime itself.

**Pyodide** demonstrates WASM viability for AI coding agents—it provides a complete Python environment (including NumPy, Pandas, Matplotlib) running entirely in the browser. This **shifts security responsibility to the user's browser sandbox**, eliminating server-side security concerns. For data analysis and visualization tasks, this architecture is ideal: agents generate Python code, submit it to browser-based Pyodide, and display results without server-side execution risks.

**WASM performance** approaches native speed (within 10-20% for CPU-bound workloads) with instant startup (~1-10ms to instantiate modules). The memory overhead is minimal. However, **WASI** (WebAssembly System Interface) remains immature for full system programming—complex filesystem operations, networking, and subprocess management are limited or require polyfills.

For Code-Beaker, **WASM is best for specific use cases**: browser-based code execution (shift security burden), data analysis without external dependencies, portable code execution across platforms, and education/training scenarios. It's not suitable as the primary execution engine for agents needing full development environment capabilities, but could complement server-side execution for appropriate workloads.

### Linux security mechanisms for defense-in-depth

Multiple Linux kernel security features layer together for robust protection. **Seccomp** (secure computing mode) filters system calls using BPF programs. Docker's default seccomp profile blocks ~300 dangerous syscalls (kernel module loading, clock manipulation, privilege escalation). Custom profiles enable whitelisting: specify only the syscalls needed (read, write, exit, open, etc.) and deny everything else. Seccomp has negligible performance overhead (\u003c1%) and dramatically reduces attack surface.

**Linux capabilities** break root privileges into 40+ granular permissions. Default Docker drops most capabilities but retains 14 including CAP_NET_RAW (packet crafting), CAP_MKNOD (device creation), and CAP_CHOWN (ownership changes). **Best practice: drop all capabilities** with `--cap-drop=ALL` then selectively add only those required. For most code execution, zero capabilities are needed. If containers run as non-root with no setuid binaries, all capabilities can be safely dropped.

**AppArmor and SELinux** provide mandatory access control. AppArmor uses path-based profiles restricting filesystem operations, network access, and capabilities. Docker applies a default "docker-default" AppArmor profile automatically. SELinux uses label-based type enforcement with more granular control but greater configuration complexity. Either provides valuable defense-in-depth: even if seccomp and capabilities are bypassed, MAC policies can still block malicious operations.

**User namespaces** enable UID remapping where container root (UID 0) maps to unprivileged user on host (UID 100000+). This means even if an attacker gains root inside the container, they have no privileges on the host. User namespaces are underutilized but highly effective. Enable with `--userns-remap=default` in Docker daemon configuration.

**Cgroups v2** enforce resource limits at kernel level. Memory limits prevent OOM attacks, CPU quotas prevent starvation, **PID limits prevent fork bombs** (the most reliable defense), and I/O limits prevent disk exhaustion. Always set limits: `--memory=256m --cpus=0.5 --pids-limit=100`. The PID limit is non-negotiable for any platform accepting untrusted code.

### Network isolation strategies

**Network access** creates significant attack surface and data exfiltration risks. The safest approach: **no network** with `--network=none`. This works for pure computation tasks where code doesn't need external resources. For agents requiring network access (package installation, API calls), several patterns emerge:

**Whitelist-based filtering** like Claude Code implements: all network requests route through proxy server outside sandbox, agents request access to specific domains, users approve new domains, and custom proxies can enforce additional filtering. This balances functionality with security by allowing necessary network access under control.

**Private networks** isolate sandboxes from each other and the internet. Create custom bridge networks per tenant/session, block inter-container communication with firewall rules, allow outbound connections only to approved destinations, and log all network activity for audit. Kubernetes NetworkPolicy CRDs enable declarative network security.

**HTTP proxy** for package managers: configure pip, npm, cargo to use authenticated proxy, cache packages for offline availability after first download, and scan packages for malicious content before serving. This allows dependency installation while controlling what can be accessed.

For Code-Beaker, **default to network=none**, provide explicit API for requesting network access (agents call `enable_network(allowed_domains=['pypi.org', 'npmjs.com'])`, and implement allowlist-based filtering for approved scenarios. Monitor network usage for anomaly detection—unexpected destinations or traffic volumes indicate compromise.

### Resource limits and attack mitigation

**Resource exhaustion** attacks are trivial without proper limits. **Fork bombs** (`:(){ :|: & };:` in bash) exponentially spawn processes until system crashes. Defense: cgroups PID limit (`--pids-limit=100`) which cleanly fails process creation when limit reached. **Memory bombs** allocate until OOM. Defense: `--memory=256m --memory-swap=256m` (setting swap equal to memory disables swap usage). **CPU exhaustion**: spin loops consume CPU. Defense: `--cpus=0.5` limits CPU time available. **Disk bombs**: write until filesystem full. Defense: disk quotas and read-only root filesystem.

**Infinite loops** require external timeout enforcement. Linux provides `timeout` command: `timeout 30s python script.py` sends SIGTERM after 30s, then SIGKILL after grace period. Judge0 and Piston implement separate compile and run timeouts (10s compile, 3s run typical). Agents need longer timeouts for complex builds, but 5-minute maximum is reasonable for most tasks.

**Output bombs** fill buffers by printing massive output. Defense: **limit stdout/stderr** buffer sizes (Piston defaults to 1024 characters). Truncate output beyond limits and notify agent that output was truncated. This prevents memory exhaustion while preserving useful debugging information.

**Compression bombs** (zip files expanding to gigabytes) are mitigated by filesystem quotas and proactive scanning. Before extracting archives, check compressed size ratios. Limit maximum extracted size and reject suspicious archives.

### Security decision framework

**For public-facing code execution** (untrusted users, unknown code): Use **gVisor or Firecracker** for strong isolation, implement comprehensive resource limits, default deny network access with whitelist option, enable all Linux security mechanisms (seccomp, capabilities, AppArmor/SELinux), run as non-root with user namespaces, and implement rate limiting per user/IP.

**For semi-trusted multi-tenant** (authenticated users, internal applications): Use **hardened Docker** with dropped capabilities and custom seccomp, set resource limits aggressively, restricted network with proxy filtering, comprehensive monitoring and audit logging, and automatic sandbox cleanup after use.

**For trusted single-tenant** (internal development, trusted agents): Use **standard Docker** with basic hardening, reasonable resource limits to prevent accidents (not attacks), network access as needed, and focus on observability over strict isolation.

Code-Beaker should **support multiple security profiles**: "paranoid" mode for public execution (gVisor/Firecracker, maximum restrictions), "balanced" mode for typical use (hardened Docker, standard limits), and "permissive" mode for development (minimal restrictions, easier debugging).

## Architecture recommendations synthesized from existing systems

Successful code execution platforms converge on common architectural patterns regardless of implementation language or specific use cases. These patterns emerge from solving universal problems: language-agnostic design, scalable execution, real-time communication, stateful vs stateless trade-offs, and security isolation.

### Modular client-server separation

The **LSP/DAP pattern** of clean client-server separation with language-agnostic protocol proves universally valuable. Code-Beaker should separate into **three components**: client SDKs in multiple languages, protocol specification defining wire format, and execution engine(s) implementing sandboxed runtime. This enables polyglot clients (Python, JavaScript, C# agents) to interact with unified execution infrastructure.

Judge0's architecture demonstrates scaling benefits: **separate API server** (Rails) handles HTTP requests and authentication, **worker pool** (isolate sandbox instances) executes code, **Redis queue** mediates between them enabling horizontal scaling of workers independently from API servers. This architecture handles thousands of concurrent executions with commodity hardware.

**Execution engines should be workers** that register with a coordinator, poll for work from queue, execute in isolated sandbox, upload results to storage, and report completion. This allows dynamic scaling based on load—spin up additional workers during peak usage, drain and terminate during low usage. For Code-Beaker, containerized workers in Kubernetes with HPA (Horizontal Pod Autoscaler) provides automatic scaling.

### Hybrid API architecture

**REST for stateless operations** (submit execution, check status, list languages) provides simplicity and caching benefits. REST scales effortlessly behind load balancers and CDNs. Use standard HTTP status codes (202 Accepted for async submission, 200 OK for completion, 429 Too Many Requests for rate limiting) and follow RESTful conventions (GET for reads, POST for writes, idempotent operations where possible).

**WebSocket for stateful operations** (streaming output, multi-step interactions, debug sessions) enables real-time bidirectional communication. Maintain WebSocket connections per session, multiplex multiple channels over single connection (control, stdout, stderr, status), implement heartbeat for connection health, and gracefully handle reconnection with session recovery.

**JSON-RPC 2.0** as message format within WebSocket provides structured RPC with request IDs, error codes, and clear semantics. This combines the benefits of REST (structured, stateless requests) with WebSocket's real-time capabilities. Piston demonstrates this hybrid successfully: REST API for simple executions, WebSocket API with JSON-RPC messages for interactive scenarios.

Code-Beaker should **expose both interfaces**: REST for maximum compatibility and simple clients, WebSocket for rich clients needing real-time features, shared authentication and authorization, session tokens usable in both protocols. This "progressive enhancement" pattern serves simple and advanced use cases without compromising either.

### Multi-channel communication model

**Separate control, data, and status channels** as Jupyter demonstrates with five sockets. Code-Beaker doesn't need five distinct channels, but should separate:

**Control channel**: start/stop/kill sandbox, set configuration, request resource usage, authentication and authorization, and timeout commands that cannot block behind long-running operations.

**Data channel(s)**: stdout/stderr streams (optionally separate), execution results (return values, exit codes), file upload/download, and high-throughput outputs that shouldn't congest control channel.

**Status/event channel**: progress notifications (compilation started, tests running), resource usage updates (CPU, memory consumption), error events (timeout, OOM), and broadcast messages that clients can subscribe to.

This separation prevents slow operations from blocking critical commands. If an agent submits a long build and needs to kill it, the kill command shouldn't queue behind build output—it uses the control channel for immediate delivery.

**Implementation**: In WebSocket architecture, use message types to route to appropriate handlers. In REST+WebSocket hybrid, control operations use REST endpoints, streaming data uses WebSocket. Include `channel` field in JSON-RPC messages to indicate routing.

### Capabilities-based feature negotiation

**LSP-inspired initialization** establishes mutual understanding of supported features. The handshake flow:

1. Client sends `initialize` request with client capabilities (supported message types, features, extensions)
2. Server responds with server capabilities (available languages, tools, resource limits, optional features)
3. Client sends `initialized` notification acknowledging handshake
4. Normal operation begins with both sides knowing what to expect

This enables **graceful degradation**—new features deploy without breaking old clients, lightweight clients omit expensive features, and servers advertise actual capabilities not assumed ones. Code-Beaker sandboxes might support debugger access only for certain languages, or linting only if tools installed—capabilities advertise this upfront.

**Capability structure** for Code-Beaker:

**Client capabilities**: supported output formats (plain text, MIME bundles), maximum message size, supported authentication methods, requested features (debugging, hot reload, network access), and preferred transport (REST only, WebSocket preferred).

**Server capabilities**: available languages and versions, installed tools (linters, debuggers, package managers), resource limits (CPU, memory, timeout, disk space), supported operations (multi-file, debugging, port forwarding), and enabled extensions.

**Dynamic capability registration**: Servers can register capabilities after initialization (debugger attached, new language installed). This allows progressive feature enabling without restarting connections.

### Session management and lifecycle

**Support both stateless and stateful modes**. Stateless (Judge0 pattern): each execution is independent, tracked by submission token, no state persists between executions, scales perfectly, and simplifies failure handling. Stateful (Jupyter pattern): sandbox maintains execution state, variables persist, execution counter increments, and enables REPL-like interactions.

**Stateful sessions** require explicit lifecycle management: CREATE session receiving session_id, EXECUTE commands within session referencing session_id, PAUSE/RESUME for long-lived sessions, DELETE session cleaning up resources, and TIMEOUT for automatic cleanup of abandoned sessions.

**State persistence** enables advanced workflows: variables carry across commands (agents can `result = analyze_data()` then later `plot(result)`), filesystem modifications persist, background processes continue running, and debugging state (breakpoints, watches) persists. This maps naturally to AI agent mental models of interacting with an environment.

Code-Beaker should **default to stateless for simplicity**, provide **optional sessions** via `create_session` API, implement **session timeout** (30-60 minutes idle), allow **explicit cleanup** (`delete_session`), and support **session persistence** (save state, resume later) for long-running agent workflows.

### Language-agnostic abstractions

**File-based interfaces** rather than language-specific: submit code as files array (`{name: 'main.py', content: '...', encoding: 'utf8'}`), support multiple files for projects, allow arbitrary file names and directory structures, and use MIME types for language detection.

**Position-based addressing**: identify code locations by line/column rather than AST nodes, maintain language neutrality, remain stable across parse tree changes, and match how developers think about code.

**Structured status codes**: SUCCESS, TIMEOUT, ERROR, KILLED, COMPILE_ERROR, RUNTIME_ERROR—enumerated codes enable reliable agent logic rather than parsing strings.

**MIME bundles for outputs**: return `{text/plain: '...', application/json: {...}}` allowing clients to choose preferred format. This supports simple text clients while enabling rich visualization in advanced clients.

**Error information structure**: status code, phase (compile vs run), exit code, signal (if killed), stdout/stderr, human-readable message, and technical details. Consistent error format across languages simplifies agent error handling.

### Extension mechanisms

**Package-based language support** like Piston demonstrates: each language is a package with metadata (name, version, dependencies), build scripts (compile from source or download binaries), runtime configuration (environment variables, paths), and capability declarations.

**Package manager (ppman pattern)**: install runtimes dynamically, support multiple versions concurrently, SemVer selection (`python@3.11.x`), dependency resolution, and cache installed packages for reuse. This allows Code-Beaker to start with minimal languages installed and expand based on actual usage.

**Configuration-based extension** for tools: define linters, formatters, debuggers as tool definitions, specify language applicability, declare required files/packages, and provide invocation patterns. This makes Code-Beaker extensible without code changes.

**Plugin system** for advanced customization: expose API for custom preprocessors/postprocessors, sandbox initialization hooks, custom command definitions, and monitoring/logging extensions. Use well-defined interfaces, isolated execution contexts, and security boundaries around plugin code.

## Trade-offs and decision framework for Code-Beaker

Code-Beaker's architecture must navigate fundamental tensions between competing goals. The decisions made determine operational characteristics, security posture, performance profile, and developer experience.

### Security vs functionality spectrum

**Maximum security** (public untrusted code execution): Firecracker microVMs with dedicated kernels, no network access by default, all Linux security mechanisms enabled, comprehensive resource limits, short execution timeouts (5 min), no privileged operations ever, and complete sandbox isolation. This enables safe public code execution APIs but limits functionality and increases operational complexity.

**High security** (multi-tenant authenticated users): gVisor for userspace kernel, whitelist-based network access, hardened Docker configuration, aggressive resource limits, moderate timeouts (15 min), and per-user sandboxes. This balances security with functionality for SaaS applications where users are authenticated but not fully trusted.

**Medium security** (trusted but verified): Standard Docker with hardening, capability dropping, resource limits to prevent accidents, reasonable network access, longer timeouts (30 min), and monitoring for anomalies. This works for enterprise deployments where users are employees or trusted partners.

**Minimal security** (development environments): Local process execution with basic isolation, permissive resource limits, full network access, no timeouts, and comprehensive debugging access. This optimizes for developer experience in trusted environments.

Code-Beaker should **implement security profiles**: "public" (Firecracker/gVisor), "standard" (hardened Docker), "development" (minimal restrictions). Allow configuration per deployment, with clear documentation of threat models and guarantees for each profile.

### Performance vs isolation trade-offs

**Firecracker microVMs**: 125ms startup, 5MB overhead, strongest isolation. Use for maximum security requirements, acceptable for synchronous executions, ideal for long-running sessions (\u003e1 minute), and scales to thousands per host. Trade-off: Each needs kernel and root filesystem images, more complex operational infrastructure.

**gVisor**: \u003c100ms startup, ~10MB overhead, 10-30% performance impact, strong isolation. Use for default configuration in multi-tenant scenarios, good balance of security and performance, proven at Google Cloud scale, and relatively simple operations (Docker runtime). Trade-off: Not 100% syscall compatible, filesystem operations can be slower.

**Hardened Docker**: \u003c1s startup, ~10MB overhead, \u003c5% performance impact, moderate isolation. Use for trusted environments, maximum performance requirements, development and testing, and simplest operations. Trade-off: Shares kernel with host, container escapes possible.

**WASM**: \u003c10ms startup, \u003c1MB overhead, 10-20% CPU overhead, excellent isolation. Use for client-side execution, data analysis without I/O, browser-based coding environments, and portable execution. Trade-off: Limited system access, immature WASI, not suitable for full development environments.

**Decision framework**: For response time requirements \u003c100ms, prefer gVisor or WASM. For workloads \u003e5 minutes, Firecracker overhead amortizes well. For maximum throughput, hardened Docker. For strongest security, Firecracker. Code-Beaker should support multiple backends with unified API.

### Stateless vs stateful execution models

**Stateless advantages**: Perfect horizontal scaling (any worker handles any request), simple failure recovery (retry on different worker), no session management complexity, easier caching and CDN distribution, and no state cleanup concerns. Ideal for: one-off code executions, testing/validation, education platforms, and public APIs.

**Stateful advantages**: REPL-like interactions (variables persist), background processes continue running, debugging sessions with state, incremental development workflows, and matches agent mental models of environment interaction. Ideal for: AI coding agents, interactive development, long-running projects, and complex multi-step tasks.

**Hybrid approach**: Default to stateless for simple cases, expose `create_session()` for stateful needs, implement reasonable session timeouts (30-60 min idle), provide explicit cleanup APIs, and support session serialization for long-term persistence. This serves both simple and complex use cases without forcing all clients into one model.

Code-Beaker should make stateless the default (simpler, scales better) but support sessions as first-class feature for agents that need persistent environments. Session IDs in requests route to sticky workers while stateless requests load balance freely.

### Custom commands vs raw shell access

**SWE-agent research proves custom commands outperform by ~20%**. Raw shell provides maximum flexibility but confuses LLMs and creates security nightmares (parsing complex outputs, chaining commands unsafely, unclear working directory, ambiguous failure modes). Custom commands provide structured interfaces (clear parameters, explicit return values, predictable behavior, error handling built-in).

**Custom command examples**: `edit_file(path, start_line, end_line, new_content)` returns success/failure explicitly. `search_directory(pattern, path)` returns structured list of matches. `run_tests(test_path)` returns pass/fail with structured results. `install_package(package_name, manager='pip')` handles different package managers uniformly.

However, **raw shell remains necessary** for flexibility: installing system packages, running arbitrary build commands, executing tools not wrapped in custom commands, and debugging when custom commands are insufficient. The solution: **hybrid approach** with custom commands as primary interface and raw shell as escape hatch.

**Agent-Computer Interface (ACI) principles** from SWE-agent: Commands return explicit feedback (never empty strings). Working directory always visible and explicit. File operations show confirmation (line numbers updated, bytes written). Error messages are structured and actionable. Output is chunked appropriately (100 lines for file viewing). Linting runs automatically with each edit.

Code-Beaker should **implement core custom commands** for file operations, testing, linting, searching, and package management. Provide **raw shell as `execute_command()`** for arbitrary operations. Design commands specifically for LLM consumption with clear JSON responses and explicit success indicators.

### Synchronous vs asynchronous execution

**Synchronous (blocking) execution**: Client submits code, waits for completion, receives results in response. Simple client code, predictable behavior, no polling needed, and HTTP request-response maps naturally. Works well for: fast operations (\u003c5 seconds), small-scale usage, simple client implementations, and test cases.

**Asynchronous (non-blocking) execution**: Client submits code receiving token, polls for status, retrieves results when complete. Scales to long operations, client can do other work while waiting, timeouts don't affect HTTP connection, and supports webhooks for completion notification. Required for: long operations (\u003e30 seconds), high-volume services, complex workflows, and production deployments.

Judge0 elegantly **supports both**: default POST returns 202 with token, client polls GET /submissions/:token, optional `wait=true` parameter makes POST synchronous up to timeout. This serves simple and advanced clients with one API.

Code-Beaker should **default to async** (more scalable) but support **synchronous mode** via `?wait=true&timeout=10` parameter. For WebSocket connections, natural flow is synchronous over persistent connection. This flexibility accommodates different client preferences and use cases.

### Polling vs streaming vs webhooks

**Polling (Judge0 pattern)**: Client repeatedly GET status until complete. Simple to implement, works with pure REST, requires no special infrastructure, but inefficient (many requests for single execution), introduces delays (poll interval determines latency), wastes resources on both sides.

**Streaming (Piston WebSocket)**: Persistent connection streams output as it happens. Real-time feedback, efficient (single connection), enables stdin interaction, natural for long-running operations, but requires WebSocket infrastructure, more complex client code, and connection management overhead.

**Webhooks (Judge0 optional)**: Client provides callback URL, server POSTs results when complete. No polling needed, works with stateless clients, integrates with event-driven architectures, but requires publicly accessible callback endpoint, security considerations (webhook authentication), and retry/reliability mechanisms.

**Recommendation for Code-Beaker**: Support all three to maximize compatibility. REST API with polling for simple clients. WebSocket streaming for rich interactive clients. Webhook callbacks for integration with workflow systems. Let clients choose based on their infrastructure and requirements.

### Multi-tenancy and resource allocation

**Dedicated sandbox per user**: Maximum isolation, no cross-tenant leakage, simple reasoning about security, but high resource usage and slower provisioning. Suitable for: paid tiers, long-running sessions, and security-critical applications.

**Shared infrastructure with isolation**: Multiple users' sandboxes on same hosts but isolated. Better resource utilization, faster scaling, lower costs, but requires robust isolation (gVisor/Firecracker), careful resource limiting, and monitoring for noisy neighbors.

**Pooled sandboxes**: Pre-warmed sandbox pool, assigned on demand, returned after use, cleaned between users. Fastest provisioning (\u003c100ms to assign), efficient resource use, and good for burst traffic. Requires thorough cleanup between uses, potential for state leakage if cleanup fails, and pool sizing tuning.

**Recommendation**: Implement **tiered approach**. Free/public tier: Shared infrastructure with gVisor, aggressive timeouts, limited resources. Paid tier: Dedicated sandboxes, longer timeouts, more resources. Enterprise: Dedicated clusters or on-premise deployment. Use **pre-warmed pools** for all tiers to reduce latency—clean thoroughly between uses.

## Practical implementation roadmap

Code-Beaker should follow a phased approach building core functionality first, then adding sophisticated features based on actual usage patterns and user feedback.

### Phase 1: Core execution engine (weeks 1-4)

**Foundation**: Implement basic REST API with synchronous execution, single-file code submission, Docker-based sandboxing with basic hardening (non-root, resource limits), Python and Node.js support, stdout/stderr capture, simple error reporting, and submission token tracking.

**Critical features**: `POST /execute` with language, code, files array. Resource limits: memory, CPU, timeout, PIDs. Basic auth with API keys. Error taxonomy: SUCCESS, TIMEOUT, ERROR, KILLED. Health check endpoint. Structured JSON responses.

**Goal**: MVP that can safely execute Python and Node.js code with basic security. Focus on correctness and safety over performance. This foundation supports simple AI agent use cases (data analysis, algorithm testing).

### Phase 2: Language expansion and tooling (weeks 5-8)

**Language support**: Add C#, Java, Rust, Go, Ruby. Implement language detection from file extensions. Support multi-file projects (extract into working directory). Add compilation step for compiled languages with separate timeouts.

**Tooling integration**: Package manager support (pip, npm, cargo). Basic linting (pylint, eslint). Test runner integration (pytest, jest). Environment variable setting. Working directory management.

**Custom commands**: Start implementing ACI concept. `edit_file()`, `read_file()`, `list_directory()`, `search_code()`, `run_tests()`, `install_package()`. Each returns structured JSON with explicit success/failure.

**Goal**: Support common programming languages and basic development workflows. Enable agents to work with multi-file projects and use standard development tools.

### Phase 3: Real-time communication (weeks 9-12)

**WebSocket API**: Implement persistent connections, JSON-RPC message format, streaming stdout/stderr as execution happens, multi-channel architecture (control, data, status), session management (create, execute, destroy).

**Interactive features**: Stdin support for user input, long-running process management, background services (web servers), output chunking and buffering, connection recovery and reconnection.

**Capabilities negotiation**: Initialization handshake, client and server capability advertisement, dynamic capability registration, graceful degradation for missing features.

**Goal**: Enable rich interactive development workflows. Support agents that need real-time feedback, multi-step interactions, and persistent sessions.

### Phase 4: Security hardening (weeks 13-16)

**Sandbox upgrades**: Implement gVisor runtime option, evaluate Firecracker for enterprise tier, custom seccomp profiles per language, capability dropping (all caps by default), user namespace remapping.

**Network isolation**: Default deny networking, whitelist-based domain access, proxy implementation for filtering, network usage monitoring, egress logging.

**Enhanced monitoring**: Resource usage tracking and alerts, anomaly detection (unusual CPU/network patterns), audit logging (all executions, all network requests), rate limiting per user/IP, execution history and replay.

**Goal**: Production-grade security suitable for multi-tenant SaaS. Pass security audits. Support enterprise compliance requirements.

### Phase 5: Advanced features (weeks 17-20)

**Debugging support**: Expose debugger APIs (pdb, node inspect), programmatic breakpoint setting, variable inspection APIs, step execution control, stack trace analysis.

**Port forwarding**: Detect services binding to ports, generate authenticated URLs, reverse proxy implementation, SSL/TLS termination, support for multiple concurrent forwards.

**Git integration**: Clone repositories, commit changes, branch management, diff generation, GitHub/GitLab API integration for PR creation.

**Performance optimization**: Implement sandbox pooling for fast provisioning, caching compiled artifacts, optimizing file I/O, reducing cold start times, horizontal scaling of workers.

**Goal**: Feature parity with production AI coding platforms. Support sophisticated agent workflows including debugging and web development.

### Phase 6: Operational excellence (weeks 21-24)

**Observability**: Comprehensive metrics (execution counts, latency, resource usage), distributed tracing, structured logging, error aggregation, user-facing dashboards.

**Reliability**: Implement retry mechanisms, graceful degradation under load, circuit breakers for failing components, automatic scaling policies, chaos engineering tests.

**Developer experience**: Create SDKs for Python, JavaScript, C#, comprehensive API documentation, example agents and tutorials, sandbox templates for common stacks, CLI tool for local development.

**Goal**: Production-ready service that's pleasant to use and operate. Enable developers to quickly integrate Code-Beaker into their AI applications.

## Specific architectural recommendations for Code-Beaker

Based on all research findings, here are concrete architectural decisions Code-Beaker should make:

**Core architecture**: Adopt LSP-inspired client-server model with clean separation. Use JSON-RPC 2.0 over multiple transports (HTTP for REST, WebSocket for real-time). Implement multi-channel communication (control, data, status). Support capabilities-based negotiation. Build modular with swappable sandbox backends.

**Execution model**: Default to stateless async execution with token-based tracking. Support optional stateful sessions with explicit lifecycle. Implement both two-phase (compile then run) and single-phase execution. Separate compile timeout (10s default) and run timeout (configurable, 30s-5min).

**API design**: REST API for CRUD operations on executions, language listing, health checks. WebSocket API for streaming output, interactive sessions, debugging. Expose both `/execute` (stateless) and `/sessions/:id/execute` (stateful). Support synchronous mode with `?wait=true&timeout=N` parameter.

**Security defaults**: Start with hardened Docker, migrate to gVisor for production. Run as non-root (UID 1000+), drop all capabilities, apply custom seccomp profiles. Default deny networking, opt-in whitelist. Set comprehensive resource limits: memory 256MB, CPU 0.5 cores, PIDs 100, timeout 30s. Implement rate limiting (100 executions/hour/user).

**Language support**: Begin with Python, Node.js, C#. Add languages via package manager system. Support multiple versions concurrently (python@3.11, python@3.12). Language metadata includes name, version, aliases, available tools (linters, debuggers), default resource limits.

**Custom commands**: Implement ACI pattern with core commands: `file.read()`, `file.write()`, `file.edit()`, `directory.list()`, `code.search()`, `tests.run()`, `package.install()`, `lint.check()`. Each returns structured response: `{success: bool, data: {}, error: {code, message}}`. Provide raw shell via `shell.execute()` as escape hatch.

**Error handling**: Define status taxonomy: SUCCESS, TIMEOUT, COMPILE_ERROR, RUNTIME_ERROR, RESOURCE_LIMIT, PERMISSION_DENIED. Structure errors: `{status, phase, exitCode, signal, stdout, stderr, message, details}`. Include request IDs in all responses for tracing.

**Extensibility**: Plugin system for custom preprocessors, postprocessors, and tools. Configuration-based language definitions. Template system for pre-configured sandboxes (python-data-science, node-web-app, rust-embedded). Webhook support for completion notifications.

**Observability**: Structured logging with execution context. Metrics for latency, success rates, resource usage. Distributed tracing across components. Audit logs for security events. User-facing execution history and logs.

**Operations**: Containerized deployment (Docker/Kubernetes). Horizontal scaling of workers. Health checks and readiness probes. Graceful shutdown with request draining. Configuration via environment variables. Secrets management integration.

## Conclusion: synthesizing best practices into actionable guidance

The research across commercial platforms, open-source projects, code execution engines, communication protocols, and AI agent requirements reveals remarkably consistent patterns. **Successful code execution environments for AI agents share a common DNA**: structured APIs over raw access, strong isolation with comprehensive resource limits, multi-channel communication, language-agnostic abstractions, capabilities-based negotiation, and careful security-functionality trade-offs.

**The most important insight**: Custom command interfaces designed for LLM comprehension outperform raw shell access by approximately 20%. SWE-agent's Agent-Computer Interface breakthrough shows that how you expose capabilities matters as much as what capabilities you provide. Code-Beaker should prioritize building structured, explicit, feedback-rich APIs rather than just providing shell access and assuming agents will figure it out.

**The security imperative**: Container isolation alone is insufficient for multi-tenant environments. Default Docker configurations are dangerously permissive. Either implement comprehensive hardening (drop all capabilities, custom seccomp, user namespaces, aggressive resource limits) or adopt userspace kernels like gVisor. For maximum security requirements, Firecracker microVMs provide hardware isolation with acceptable overhead.

**The architecture pattern**: JSON-RPC over HTTP and WebSocket, inspired by LSP/DAP/Jupyter, provides the best foundation. Capabilities-based negotiation enables graceful evolution. Multi-channel separation prevents control flow blockage. Language-agnostic abstractions (files, positions, MIME types) maximize compatibility. This architecture is proven at scale across multiple successful systems.

**The practical path**: Build incrementally. Start with hardened Docker executing Python/Node.js via REST API with synchronous and async modes. Add custom commands layer. Implement WebSocket for streaming. Introduce sessions for stateful workflows. Upgrade to gVisor for production security. Add debugging and advanced features based on actual user needs. Don't prematurely optimize—many features seem essential but are rarely used in practice.

**The decision framework**: Choose sandbox technology based on threat model (public/untrusted → Firecracker, multi-tenant → gVisor, internal → hardened Docker, development → minimal restrictions). Implement security profiles, not one-size-fits-all. Support both stateless and stateful execution. Provide multiple communication patterns (REST, WebSocket, webhooks). Make defaults secure but provide escape hatches for power users.

Code-Beaker has the opportunity to synthesize lessons from a decade of code execution platforms and two years of AI coding agent evolution. By adopting proven patterns—structured commands, strong isolation, multi-channel communication, capabilities negotiation—while avoiding common pitfalls—raw shell access, weak sandboxing, monolithic architecture—Code-Beaker can provide the foundation that next-generation AI coding agents need to safely and effectively write, test, and deploy software.