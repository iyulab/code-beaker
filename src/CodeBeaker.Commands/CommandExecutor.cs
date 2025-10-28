using System.Diagnostics;
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
    private readonly DockerClient _docker;

    public CommandExecutor(DockerClient docker)
    {
        _docker = docker;
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
        // Use 'tee' command to write file (no shell parsing overhead)
        var execConfig = new ContainerExecCreateParameters
        {
            Cmd = new[] { "tee", command.Path },
            AttachStdin = true,
            AttachStdout = true,
            AttachStderr = true,
            WorkingDir = "/workspace"
        };

        var execResponse = await _docker.Exec.ExecCreateContainerAsync(containerId, execConfig, cancellationToken);

        using var stream = await _docker.Exec.StartAndAttachContainerExecAsync(
            execResponse.ID,
            tty: false,
            cancellationToken);

        // Write content to stdin
        var bytes = Encoding.UTF8.GetBytes(command.Content);
        await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken);

        return CommandResult.Ok(new { path = command.Path, bytes = bytes.Length });
    }

    /// <summary>
    /// Read file using Docker Exec
    /// </summary>
    private async Task<CommandResult> ExecuteReadFileAsync(
        ReadFileCommand command,
        string containerId,
        CancellationToken cancellationToken)
    {
        var execConfig = new ContainerExecCreateParameters
        {
            Cmd = new[] { "cat", command.Path },
            AttachStdout = true,
            AttachStderr = true,
            WorkingDir = "/workspace"
        };

        var execResponse = await _docker.Exec.ExecCreateContainerAsync(containerId, execConfig, cancellationToken);

        using var stream = await _docker.Exec.StartAndAttachContainerExecAsync(
            execResponse.ID,
            tty: false,
            cancellationToken);

        var output = await ReadStreamAsync(stream, cancellationToken);

        return CommandResult.Ok(new { path = command.Path, content = output.Stdout });
    }

    /// <summary>
    /// Create directory using Docker Exec
    /// </summary>
    private async Task<CommandResult> ExecuteCreateDirectoryAsync(
        CreateDirectoryCommand command,
        string containerId,
        CancellationToken cancellationToken)
    {
        var args = command.Recursive
            ? new[] { "mkdir", "-p", command.Path }
            : new[] { "mkdir", command.Path };

        var execConfig = new ContainerExecCreateParameters
        {
            Cmd = args,
            AttachStdout = true,
            AttachStderr = true,
            WorkingDir = "/workspace"
        };

        var execResponse = await _docker.Exec.ExecCreateContainerAsync(containerId, execConfig, cancellationToken);
        using var stream = await _docker.Exec.StartAndAttachContainerExecAsync(execResponse.ID, false, cancellationToken);

        var output = await ReadStreamAsync(stream, cancellationToken);

        return CommandResult.Ok(new { path = command.Path });
    }

    /// <summary>
    /// Copy file using Docker Exec
    /// </summary>
    private async Task<CommandResult> ExecuteCopyFileAsync(
        CopyFileCommand command,
        string containerId,
        CancellationToken cancellationToken)
    {
        var args = command.Overwrite
            ? new[] { "cp", "-f", command.Source, command.Destination }
            : new[] { "cp", command.Source, command.Destination };

        var execConfig = new ContainerExecCreateParameters
        {
            Cmd = args,
            AttachStdout = true,
            AttachStderr = true,
            WorkingDir = "/workspace"
        };

        var execResponse = await _docker.Exec.ExecCreateContainerAsync(containerId, execConfig, cancellationToken);
        using var stream = await _docker.Exec.StartAndAttachContainerExecAsync(execResponse.ID, false, cancellationToken);

        var output = await ReadStreamAsync(stream, cancellationToken);

        return CommandResult.Ok(new { source = command.Source, destination = command.Destination });
    }

    /// <summary>
    /// Execute shell command directly (optimized path, no shell wrapper)
    /// </summary>
    private async Task<CommandResult> ExecuteShellAsync(
        ExecuteShellCommand command,
        string containerId,
        CancellationToken cancellationToken)
    {
        var cmd = new List<string> { command.CommandName };
        cmd.AddRange(command.Args);

        var execConfig = new ContainerExecCreateParameters
        {
            Cmd = cmd.ToArray(),
            AttachStdout = true,
            AttachStderr = true,
            WorkingDir = command.WorkingDirectory ?? "/workspace",
            Env = command.Environment?.Select(kv => $"{kv.Key}={kv.Value}").ToList()
        };

        var execResponse = await _docker.Exec.ExecCreateContainerAsync(containerId, execConfig, cancellationToken);
        using var stream = await _docker.Exec.StartAndAttachContainerExecAsync(execResponse.ID, false, cancellationToken);

        var output = await ReadStreamAsync(stream, cancellationToken);

        // Get exit code
        var inspect = await _docker.Exec.InspectContainerExecAsync(execResponse.ID, cancellationToken);

        return CommandResult.Ok(new
        {
            stdout = output.Stdout,
            stderr = output.Stderr,
            exitCode = inspect.ExitCode
        });
    }

    /// <summary>
    /// Read multiplexed Docker stream
    /// </summary>
    private async Task<(string Stdout, string Stderr)> ReadStreamAsync(
        MultiplexedStream stream,
        CancellationToken cancellationToken)
    {
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        var buffer = new byte[4096];

        while (true)
        {
            var result = await stream.ReadOutputAsync(buffer, 0, buffer.Length, cancellationToken);

            if (result.EOF)
            {
                break;
            }

            var text = Encoding.UTF8.GetString(buffer, 0, result.Count);

            if (result.Target == MultiplexedStream.TargetStream.StandardOut)
            {
                stdout.Append(text);
            }
            else if (result.Target == MultiplexedStream.TargetStream.StandardError)
            {
                stderr.Append(text);
            }
        }

        return (stdout.ToString(), stderr.ToString());
    }

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
            var findCommand = BuildFindCommand(command);

            var execConfig = new ContainerExecCreateParameters
            {
                Cmd = new[] { "sh", "-c", findCommand },
                AttachStdout = true,
                AttachStderr = true,
                WorkingDir = "/workspace"
            };

            var execResponse = await _docker.Exec.ExecCreateContainerAsync(containerId, execConfig, cancellationToken);
            using var stream = await _docker.Exec.StartAndAttachContainerExecAsync(execResponse.ID, false, cancellationToken);

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
    /// Build find command with filters
    /// </summary>
    private string BuildFindCommand(ListFilesCommand command)
    {
        var parts = new List<string> { "find", command.Path };

        // Add max depth
        if (!command.Recursive)
        {
            parts.Add("-maxdepth 1");
        }
        else if (command.MaxDepth > 0)
        {
            parts.Add($"-maxdepth {command.MaxDepth}");
        }

        // Exclude hidden files
        if (!command.IncludeHidden)
        {
            parts.Add(@"-not -path '*/\.*'");
        }

        // Add pattern filter
        if (!string.IsNullOrEmpty(command.Pattern))
        {
            parts.Add($"-name '{command.Pattern}'");
        }

        // Output format: type|size|mtime|path
        parts.Add(@"-printf '%y|%s|%T@|%p\n'");

        return string.Join(" ", parts);
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
