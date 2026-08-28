using System.Text;
using CodeBeaker.Commands.Interfaces;
using Docker.DotNet;

namespace CodeBeaker.Commands;

/// <summary>
/// Default <see cref="IContainerExecStreamIO"/> that reads and writes a real Docker
/// multiplexed exec stream.
/// </summary>
public sealed class DockerContainerExecStreamIO : IContainerExecStreamIO
{
    public Task WriteInputAsync(MultiplexedStream stream, byte[] content, CancellationToken cancellationToken)
        => stream.WriteAsync(content, 0, content.Length, cancellationToken);

    public async Task<(string Stdout, string Stderr)> ReadOutputAsync(MultiplexedStream stream, CancellationToken cancellationToken)
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
