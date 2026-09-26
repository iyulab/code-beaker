using Docker.DotNet;

namespace CodeBeaker.Commands.Interfaces;

/// <summary>
/// Reads and writes the multiplexed stdin/stdout/stderr stream produced by a Docker exec session.
/// </summary>
/// <remarks>
/// Exists as a seam around <see cref="MultiplexedStream.ReadOutputAsync"/> and
/// <see cref="MultiplexedStream.WriteAsync"/>, both of which are non-virtual and operate
/// directly on the underlying Stream, so neither can be mocked.
/// </remarks>
public interface IContainerExecStreamIO
{
    /// <summary>
    /// Writes <paramref name="content"/> to the exec's stdin, then closes stdin so the process
    /// sees the end of its input.
    /// </summary>
    Task WriteInputAsync(MultiplexedStream stream, byte[] content, CancellationToken cancellationToken);

    Task<(string Stdout, string Stderr)> ReadOutputAsync(MultiplexedStream stream, CancellationToken cancellationToken);
}
