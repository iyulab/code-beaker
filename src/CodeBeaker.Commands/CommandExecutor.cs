using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CodeBeaker.Commands.Interfaces;
using CodeBeaker.Commands.Models;
using CodeBeaker.Commands.Utilities;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace CodeBeaker.Commands;

/// <summary>
/// Command executor with Docker API integration (bypassing shell)
/// </summary>
public sealed class CommandExecutor : ICommandExecutor
{
    private readonly IDockerClient _docker;
    private readonly IContainerExecStreamIO _streamIO;

    public CommandExecutor(IDockerClient docker, IContainerExecStreamIO? streamIO = null)
    {
        _docker = docker;
        _streamIO = streamIO ?? new DockerContainerExecStreamIO();
    }

    /// <summary>
    /// Execute command with pattern matching dispatch
    /// </summary>
    public async Task<CommandResult> ExecuteAsync(
        Command command,
        string containerId,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var result = command switch
            {
                WriteFileCommand write => await ExecuteWriteFileAsync(write, containerId, cancellationToken),
                ReadFileCommand read => await ExecuteReadFileAsync(read, containerId, cancellationToken),
                CreateDirectoryCommand mkdir => await ExecuteCreateDirectoryAsync(mkdir, containerId, cancellationToken),
                CopyFileCommand copy => await ExecuteCopyFileAsync(copy, containerId, cancellationToken),
                ExecuteShellCommand shell => await ExecuteShellAsync(shell, containerId, cancellationToken),
                ListFilesCommand listFiles => await ExecuteListFilesAsync(listFiles, containerId, cancellationToken),
                DiffCommand diff => await ExecuteDiffAsync(diff, containerId, cancellationToken),
                ApplyPatchCommand applyPatch => await ExecuteApplyPatchAsync(applyPatch, containerId, cancellationToken),
                _ => throw new NotSupportedException($"Command type {command.Type} not supported")
            };

            result.Id = command.Id;
            result.DurationMs = (int)sw.ElapsedMilliseconds;
            return result;
        }
        catch (Exception ex)
        {
            return CommandResult.Fail(ex.Message, (int)sw.ElapsedMilliseconds);
        }
    }

    /// <summary>
    /// Execute commands in batch (sequential)
    /// </summary>
    public async Task<List<CommandResult>> ExecuteBatchAsync(
        IEnumerable<Command> commands,
        string containerId,
        CancellationToken cancellationToken = default)
    {
        var results = new List<CommandResult>();

        foreach (var command in commands)
        {
            var result = await ExecuteAsync(command, containerId, cancellationToken);
            results.Add(result);

            // Stop on first failure
            if (!result.Success)
            {
                break;
            }
        }

        return results;
    }

    /// <summary>
    /// Write file using Docker Exec (bypasses shell)
    /// </summary>
    private async Task<CommandResult> ExecuteWriteFileAsync(
        WriteFileCommand command,
        string containerId,
        CancellationToken cancellationToken)
    {
        byte[] bytes;
        try
        {
            bytes = command.GetContentBytes();
        }
        catch (FormatException)
        {
            return CommandResult.Fail("Failed to write file: content is not valid base64");
        }

        try
        {
            // Missing parent directories are created, as the in-process runtimes do.
            var slash = command.Path.LastIndexOf('/');
            if (slash > 0)
            {
                var mkdir = await RunExecAsync(containerId, ["mkdir", "-p", "--", command.Path[..slash]], cancellationToken);
                if (mkdir.ExitCode != 0)
                {
                    return Failure("write file", mkdir);
                }
            }

            // dd rather than tee: tee echoes everything it writes to its stdout, and a write larger
            // than the pipe buffer stalls once nobody reads that. dd writes only the file, and of=
            // is an operand, so a path beginning with "-" is still taken as a path.
            var args = new List<string> { "dd", $"of={command.Path}", "bs=64K", "status=none" };
            if (command.Mode == FileWriteMode.Append)
            {
                args.AddRange(["oflag=append", "conv=notrunc"]);
            }

            var write = await RunExecAsync(containerId, [.. args], cancellationToken, stdin: bytes);
            return write.ExitCode == 0
                ? CommandResult.Ok(new { path = command.Path, bytes = bytes.Length })
                : Failure("write file", write);
        }
        catch (Exception ex)
        {
            return CommandResult.Fail($"Failed to write file: {ex.Message}");
        }
    }

    /// <summary>
    /// Read file using Docker Exec
    /// </summary>
    private async Task<CommandResult> ExecuteReadFileAsync(
        ReadFileCommand command,
        string containerId,
        CancellationToken cancellationToken)
    {
        try
        {
            var read = await RunExecAsync(containerId, ["cat", "--", command.Path], cancellationToken);
            return read.ExitCode == 0
                ? CommandResult.Ok(new { path = command.Path, content = read.Stdout })
                : Failure("read file", read);
        }
        catch (Exception ex)
        {
            return CommandResult.Fail($"Failed to read file: {ex.Message}");
        }
    }

    /// <summary>
    /// Create directory using Docker Exec
    /// </summary>
    private async Task<CommandResult> ExecuteCreateDirectoryAsync(
        CreateDirectoryCommand command,
        string containerId,
        CancellationToken cancellationToken)
    {
        string[] args = command.Recursive
            ? ["mkdir", "-p", "--", command.Path]
            : ["mkdir", "--", command.Path];

        var mkdir = await RunExecAsync(containerId, args, cancellationToken);
        return mkdir.ExitCode == 0
            ? CommandResult.Ok(new { path = command.Path })
            : Failure("create directory", mkdir);
    }

    /// <summary>
    /// Copy file using Docker Exec
    /// </summary>
    private async Task<CommandResult> ExecuteCopyFileAsync(
        CopyFileCommand command,
        string containerId,
        CancellationToken cancellationToken)
    {
        string[] args = command.Overwrite
            ? ["cp", "-f", "--", command.Source, command.Destination]
            : ["cp", "-n", "--", command.Source, command.Destination];

        var copy = await RunExecAsync(containerId, args, cancellationToken);
        return copy.ExitCode == 0
            ? CommandResult.Ok(new { source = command.Source, destination = command.Destination })
            : Failure("copy file", copy);
    }

    /// <summary>
    /// Execute shell command directly (optimized path, no shell wrapper)
    /// </summary>
    private async Task<CommandResult> ExecuteShellAsync(
        ExecuteShellCommand command,
        string containerId,
        CancellationToken cancellationToken)
    {
        var shell = await RunExecAsync(
            containerId,
            [command.CommandName, .. command.Args],
            cancellationToken,
            workingDirectory: command.WorkingDirectory,
            environment: command.Environment?.Select(kv => $"{kv.Key}={kv.Value}").ToList());

        return CommandResult.FromShellOutput(
            new ShellCommandOutput((int)shell.ExitCode, shell.Stdout, shell.Stderr));
    }

    /// <summary>
    /// Runs one process in the container and waits for it to exit. With <paramref name="stdin"/>,
    /// that content is written to the process and its stdin closed before the output is read.
    /// </summary>
    private async Task<ExecOutcome> RunExecAsync(
        string containerId,
        string[] cmd,
        CancellationToken cancellationToken,
        byte[]? stdin = null,
        string? workingDirectory = null,
        IList<string>? environment = null)
    {
        var execConfig = new ContainerExecCreateParameters
        {
            Cmd = cmd,
            AttachStdin = stdin is not null,
            AttachStdout = true,
            AttachStderr = true,
            WorkingDir = workingDirectory ?? "/workspace",
            Env = environment ?? []
        };

        var execResponse = await _docker.Exec.CreateContainerExecAsync(containerId, execConfig, cancellationToken);
        using var stream = await _docker.Exec.StartContainerExecAsync(execResponse.ID, new ContainerExecStartParameters { TTY = false }, cancellationToken);

        if (stdin is not null)
        {
            await _streamIO.WriteInputAsync(stream, stdin, cancellationToken);
        }

        var output = await ReadStreamAsync(stream, cancellationToken);

        // The exec has finished once its output stream closes; the daemon holds the exit code.
        // A missing exit code means the daemon does not consider the exec finished; reporting
        // that as any particular exit code would turn "unknown" into success or failure.
        var inspect = await _docker.Exec.InspectContainerExecAsync(execResponse.ID, cancellationToken);
        var exitCode = inspect.ExitCode
            ?? throw new InvalidOperationException("The Docker daemon reported no exit code for the finished command");

        return new ExecOutcome(exitCode, output.Stdout, output.Stderr);
    }

    private static CommandResult Failure(string action, ExecOutcome outcome) =>
        CommandResult.Fail(outcome.Stderr.Trim() is { Length: > 0 } error
            ? $"Failed to {action}: {error}"
            : $"Failed to {action}: exited with code {outcome.ExitCode}");

    private sealed record ExecOutcome(long ExitCode, string Stdout, string Stderr);

    /// <summary>
    /// Read multiplexed Docker stream
    /// </summary>
    private Task<(string Stdout, string Stderr)> ReadStreamAsync(
        MultiplexedStream stream,
        CancellationToken cancellationToken)
        => _streamIO.ReadOutputAsync(stream, cancellationToken);

    /// <summary>
    /// Execute ListFilesCommand - Get file tree structure
    /// Phase 14: Runtime Handlers
    /// </summary>
    private async Task<CommandResult> ExecuteListFilesAsync(
        ListFilesCommand command,
        string containerId,
        CancellationToken cancellationToken)
    {
        try
        {
            // Build find command for Docker container
            var findArgs = BuildFindArgs(command);

            var execConfig = new ContainerExecCreateParameters
            {
                Cmd = findArgs,
                AttachStdout = true,
                AttachStderr = true,
                WorkingDir = "/workspace"
            };

            var execResponse = await _docker.Exec.CreateContainerExecAsync(containerId, execConfig, cancellationToken);
            using var stream = await _docker.Exec.StartContainerExecAsync(execResponse.ID, new ContainerExecStartParameters { TTY = false }, cancellationToken);

            var output = await ReadStreamAsync(stream, cancellationToken);

            if (!string.IsNullOrEmpty(output.Stderr))
            {
                return CommandResult.Fail($"Failed to list files: {output.Stderr}");
            }

            // Parse find output and build tree
            var tree = ParseFindOutput(output.Stdout, command.Path);

            return CommandResult.Ok(new { tree = tree });
        }
        catch (Exception ex)
        {
            return CommandResult.Fail($"Failed to list files: {ex.Message}");
        }
    }

    /// <summary>
    /// Build the find argv with filters.
    /// Every element becomes a separate Docker exec argument, so a value interpolated
    /// here (path, pattern) cannot be reinterpreted as shell syntax.
    /// </summary>
    private static IList<string> BuildFindArgs(ListFilesCommand command)
    {
        var args = new List<string> { "find", command.Path };

        // Add max depth
        if (!command.Recursive)
        {
            args.Add("-maxdepth");
            args.Add("1");
        }
        else if (command.MaxDepth > 0)
        {
            args.Add("-maxdepth");
            args.Add(command.MaxDepth.ToString(CultureInfo.InvariantCulture));
        }

        // Exclude hidden files
        if (!command.IncludeHidden)
        {
            args.Add("-not");
            args.Add("-path");
            args.Add("*/.*");
        }

        // Add pattern filter
        if (!string.IsNullOrEmpty(command.Pattern))
        {
            args.Add("-name");
            args.Add(command.Pattern);
        }

        // Output format: type|size|mtime|path
        args.Add("-printf");
        args.Add(@"%y|%s|%T@|%p\n");

        return args;
    }

    /// <summary>
    /// Parse find output into tree structure
    /// </summary>
    private FileTreeNode ParseFindOutput(string output, string rootPath)
    {
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var fileMap = new Dictionary<string, FileTreeNode>();

        // Create root node
        var root = new FileTreeNode
        {
            Name = rootPath == "." ? "." : Path.GetFileName(rootPath),
            Path = rootPath,
            Type = FileEntryType.Directory,
            Size = 0,
            Modified = DateTime.UtcNow,
            Children = new List<FileTreeNode>()
        };

        fileMap[rootPath] = root;

        foreach (var line in lines)
        {
            var parts = line.Split('|');
            if (parts.Length != 4) continue;

            var type = parts[0] == "d" ? FileEntryType.Directory : FileEntryType.File;
            var size = long.TryParse(parts[1], out var s) ? s : 0;
            var mtimeSeconds = double.TryParse(parts[2], out var mt) ? mt : 0;
            var path = parts[3];

            var node = new FileTreeNode
            {
                Name = Path.GetFileName(path),
                Path = path,
                Type = type,
                Size = size,
                Modified = DateTimeOffset.FromUnixTimeSeconds((long)mtimeSeconds).DateTime,
                Children = type == FileEntryType.Directory ? new List<FileTreeNode>() : null
            };

            fileMap[path] = node;

            // Add to parent's children
            var parentPath = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(parentPath) && fileMap.TryGetValue(parentPath, out var parent))
            {
                parent.Children?.Add(node);
            }
        }

        return root;
    }

    /// <summary>
    /// Execute DiffCommand - Generate unified diff
    /// Phase 14: Runtime Handlers
    /// </summary>
    private async Task<CommandResult> ExecuteDiffAsync(
        DiffCommand command,
        string containerId,
        CancellationToken cancellationToken)
    {
        try
        {
            string originalContent;
            string modifiedContent;

            // Get original content
            if (!string.IsNullOrEmpty(command.OriginalPath))
            {
                var readResult = await ExecuteReadFileAsync(
                    new ReadFileCommand { Path = command.OriginalPath },
                    containerId,
                    cancellationToken);

                if (!readResult.Success)
                {
                    return CommandResult.Fail($"Failed to read original file: {readResult.Error}");
                }

                var resultJson = JsonSerializer.Serialize(readResult.Result);
                var resultObj = JsonSerializer.Deserialize<JsonElement>(resultJson);
                originalContent = resultObj.GetProperty("content").GetString() ?? "";
            }
            else if (!string.IsNullOrEmpty(command.OriginalContent))
            {
                originalContent = command.OriginalContent;
            }
            else
            {
                return CommandResult.Fail("Either OriginalPath or OriginalContent must be provided");
            }

            // Get modified content
            if (!string.IsNullOrEmpty(command.ModifiedPath))
            {
                var readResult = await ExecuteReadFileAsync(
                    new ReadFileCommand { Path = command.ModifiedPath },
                    containerId,
                    cancellationToken);

                if (!readResult.Success)
                {
                    return CommandResult.Fail($"Failed to read modified file: {readResult.Error}");
                }

                var resultJson = JsonSerializer.Serialize(readResult.Result);
                var resultObj = JsonSerializer.Deserialize<JsonElement>(resultJson);
                modifiedContent = resultObj.GetProperty("content").GetString() ?? "";
            }
            else if (!string.IsNullOrEmpty(command.ModifiedContent))
            {
                modifiedContent = command.ModifiedContent;
            }
            else
            {
                return CommandResult.Fail("Either ModifiedPath or ModifiedContent must be provided");
            }

            // Generate diff using DiffGenerator
            var diff = DiffGenerator.GenerateUnifiedDiff(
                originalContent,
                modifiedContent,
                command.OriginalPath ?? "original",
                command.ModifiedPath ?? "modified",
                command.ContextLines);

            var stats = DiffGenerator.CalculateStats(originalContent, modifiedContent);

            var result = new DiffResult
            {
                Diff = diff,
                AddedLines = stats.added,
                RemovedLines = stats.removed,
                ModifiedLines = stats.modified,
                Identical = string.IsNullOrEmpty(diff)
            };

            return CommandResult.Ok(result);
        }
        catch (Exception ex)
        {
            return CommandResult.Fail($"Failed to generate diff: {ex.Message}");
        }
    }

    /// <summary>
    /// Execute ApplyPatchCommand - Apply unified diff patch
    /// Phase 14: Runtime Handlers
    /// </summary>
    private async Task<CommandResult> ExecuteApplyPatchAsync(
        ApplyPatchCommand command,
        string containerId,
        CancellationToken cancellationToken)
    {
        try
        {
            // Determine target file
            string targetPath;
            if (!string.IsNullOrEmpty(command.TargetPath))
            {
                targetPath = command.TargetPath;
            }
            else
            {
                // Extract from patch header
                var patches = PatchApplicator.ParseUnifiedDiff(command.Patch);
                if (patches.Count == 0)
                {
                    return CommandResult.Fail("No valid patches found in diff");
                }

                var fileName = patches[0].ModifiedFile;
                if (command.Strip > 0)
                {
                    var parts = fileName.Split('/', '\\');
                    fileName = string.Join("/", parts.Skip(command.Strip));
                }

                targetPath = fileName;
            }

            // Read original content
            var readResult = await ExecuteReadFileAsync(
                new ReadFileCommand { Path = targetPath },
                containerId,
                cancellationToken);

            if (!readResult.Success)
            {
                return CommandResult.Fail($"Target file not found: {targetPath}");
            }

            var resultJson = JsonSerializer.Serialize(readResult.Result);
            var resultObj = JsonSerializer.Deserialize<JsonElement>(resultJson);
            var originalContent = resultObj.GetProperty("content").GetString() ?? "";

            // Apply patch using PatchApplicator
            var patchResult = PatchApplicator.ApplyPatch(
                command.Patch,
                originalContent,
                command.DryRun);

            // Write modified content if not dry-run and successful
            if (!command.DryRun && patchResult.Success && !string.IsNullOrEmpty(patchResult.ModifiedContent))
            {
                var writeResult = await ExecuteWriteFileAsync(
                    new WriteFileCommand
                    {
                        Path = targetPath,
                        Content = patchResult.ModifiedContent
                    },
                    containerId,
                    cancellationToken);

                if (!writeResult.Success)
                {
                    return CommandResult.Fail($"Failed to write patched file: {writeResult.Error}");
                }
            }

            return CommandResult.Ok(patchResult);
        }
        catch (Exception ex)
        {
            return CommandResult.Fail($"Failed to apply patch: {ex.Message}");
        }
    }
}
