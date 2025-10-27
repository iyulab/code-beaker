using System.Diagnostics;
using System.Text;
using CodeBeaker.Commands.Interfaces;
using CodeBeaker.Commands.Models;
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
}
