using System.Text.Json;
using CodeBeaker.Core.Sessions;
using CodeBeaker.JsonRpc;
using CodeBeaker.JsonRpc.Handlers;
using CodeBeaker.JsonRpc.Models;
using Xunit;

namespace CodeBeaker.Integration.Tests;

/// <summary>
/// Session JSON-RPC 통합 테스트
/// </summary>
public sealed class SessionJsonRpcTests : IDisposable
{
    private readonly SessionManager _sessionManager;
    private readonly JsonRpcRouter _router;

    public SessionJsonRpcTests()
    {
        _sessionManager = new SessionManager();
        _router = new JsonRpcRouter();

        // Register session handlers
        _router.RegisterHandler(new SessionCreateHandler(_sessionManager));
        _router.RegisterHandler(new SessionExecuteHandler(_sessionManager));
        _router.RegisterHandler(new SessionCloseHandler(_sessionManager));
        _router.RegisterHandler(new SessionListHandler(_sessionManager));
    }

    [Fact]
    public async Task SessionCreate_ShouldReturnSessionInfo()
    {
        // Arrange
        var request = new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 1,
            Method = "session.create",
            Params = new
            {
                language = "python",
                idleTimeoutMinutes = 30,
                maxLifetimeMinutes = 120
            }
        };

        // Act
        var response = await _router.ProcessRequestAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.Null(response.Error);
        Assert.NotNull(response.Result);

        var json = JsonSerializer.Serialize(response.Result);
        var result = JsonSerializer.Deserialize<JsonElement>(json);

        Assert.True(result.TryGetProperty("sessionId", out var sessionId));
        Assert.False(string.IsNullOrEmpty(sessionId.GetString()));

        // Cleanup
        var closeRequest = new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 2,
            Method = "session.close",
            Params = new { sessionId = sessionId.GetString() }
        };
        await _router.ProcessRequestAsync(closeRequest);
    }

    [Fact]
    public async Task SessionExecute_ShouldExecuteCommand()
    {
        // Arrange
        // 1. Create session
        var createRequest = new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 1,
            Method = "session.create",
            Params = new { language = "python" }
        };

        var createResponse = await _router.ProcessRequestAsync(createRequest);
        var createJson = JsonSerializer.Serialize(createResponse!.Result);
        var createResult = JsonSerializer.Deserialize<JsonElement>(createJson);
        var sessionId = createResult.GetProperty("sessionId").GetString();

        // 2. Execute command
        var executeRequest = new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 2,
            Method = "session.execute",
            Params = new
            {
                sessionId,
                command = new
                {
                    type = "write_file",
                    path = "/workspace/test.txt",
                    content = "Hello JSON-RPC",
                    mode = "Create"
                }
            }
        };

        // Act
        var executeResponse = await _router.ProcessRequestAsync(executeRequest);

        // Assert
        Assert.NotNull(executeResponse);
        Assert.Null(executeResponse.Error);

        var executeJson = JsonSerializer.Serialize(executeResponse.Result);
        var executeResult = JsonSerializer.Deserialize<JsonElement>(executeJson);

        Assert.True(executeResult.TryGetProperty("success", out var success));
        Assert.True(success.GetBoolean());

        // Cleanup
        var closeRequest = new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 3,
            Method = "session.close",
            Params = new { sessionId }
        };
        await _router.ProcessRequestAsync(closeRequest);
    }

    [Fact]
    public async Task SessionList_ShouldReturnActiveSessions()
    {
        // Arrange
        // Create 2 sessions
        var create1 = new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 1,
            Method = "session.create",
            Params = new { language = "python" }
        };

        var create2 = new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 2,
            Method = "session.create",
            Params = new { language = "javascript" }
        };

        var response1 = await _router.ProcessRequestAsync(create1);
        var response2 = await _router.ProcessRequestAsync(create2);

        var json1 = JsonSerializer.Serialize(response1!.Result);
        var json2 = JsonSerializer.Serialize(response2!.Result);
        var result1 = JsonSerializer.Deserialize<JsonElement>(json1);
        var result2 = JsonSerializer.Deserialize<JsonElement>(json2);
        var sessionId1 = result1.GetProperty("sessionId").GetString();
        var sessionId2 = result2.GetProperty("sessionId").GetString();

        // Act
        var listRequest = new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 3,
            Method = "session.list",
            Params = new { }
        };

        var listResponse = await _router.ProcessRequestAsync(listRequest);

        // Assert
        Assert.NotNull(listResponse);
        Assert.Null(listResponse.Error);

        var listJson = JsonSerializer.Serialize(listResponse.Result);
        var listResult = JsonSerializer.Deserialize<JsonElement>(listJson);

        Assert.True(listResult.TryGetProperty("count", out var count));
        Assert.True(count.GetInt32() >= 2);

        Assert.True(listResult.TryGetProperty("sessions", out var sessions));
        var sessionArray = sessions.EnumerateArray().ToList();
        Assert.True(sessionArray.Count >= 2);

        // Cleanup
        await _router.ProcessRequestAsync(new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 4,
            Method = "session.close",
            Params = new { sessionId = sessionId1 }
        });

        await _router.ProcessRequestAsync(new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 5,
            Method = "session.close",
            Params = new { sessionId = sessionId2 }
        });
    }

    [Fact]
    public async Task SessionClose_ShouldCloseSession()
    {
        // Arrange
        var createRequest = new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 1,
            Method = "session.create",
            Params = new { language = "python" }
        };

        var createResponse = await _router.ProcessRequestAsync(createRequest);
        var createJson = JsonSerializer.Serialize(createResponse!.Result);
        var createResult = JsonSerializer.Deserialize<JsonElement>(createJson);
        var sessionId = createResult.GetProperty("sessionId").GetString();

        // Act
        var closeRequest = new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 2,
            Method = "session.close",
            Params = new { sessionId }
        };

        var closeResponse = await _router.ProcessRequestAsync(closeRequest);

        // Assert
        Assert.NotNull(closeResponse);
        Assert.Null(closeResponse.Error);

        var closeJson = JsonSerializer.Serialize(closeResponse.Result);
        var closeResult = JsonSerializer.Deserialize<JsonElement>(closeJson);

        Assert.True(closeResult.TryGetProperty("closed", out var closed));
        Assert.True(closed.GetBoolean());

        // Verify session is closed
        var session = await _sessionManager.GetSessionAsync(sessionId!);
        Assert.Null(session);
    }

    [Fact]
    public async Task SessionCreate_WithInvalidParams_ShouldReturnError()
    {
        // Arrange
        var request = new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 1,
            Method = "session.create",
            Params = new { } // Missing language
        };

        // Act
        var response = await _router.ProcessRequestAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.NotNull(response.Error);
        Assert.Null(response.Result);
    }

    [Fact]
    public async Task SessionExecute_WithInvalidSessionId_ShouldReturnError()
    {
        // Arrange
        var request = new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 1,
            Method = "session.execute",
            Params = new
            {
                sessionId = "non-existent-session",
                command = new
                {
                    type = "write_file",
                    path = "/workspace/test.txt",
                    content = "Test"
                }
            }
        };

        // Act
        var response = await _router.ProcessRequestAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.NotNull(response.Error);
        Assert.Null(response.Result);
    }

    public void Dispose()
    {
        _sessionManager.Dispose();
    }
}
