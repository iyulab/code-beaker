using CodeBeaker.Commands.Models;

namespace CodeBeaker.Commands.Interfaces;

/// <summary>
/// Command execution interface
/// </summary>
public interface ICommandExecutor
{
    /// <summary>
    /// Execute a single command
    /// </summary>
    Task<CommandResult> ExecuteAsync(Command command, string containerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute multiple commands in sequence
    /// </summary>
    Task<List<CommandResult>> ExecuteBatchAsync(IEnumerable<Command> commands, string containerId, CancellationToken cancellationToken = default);
}
