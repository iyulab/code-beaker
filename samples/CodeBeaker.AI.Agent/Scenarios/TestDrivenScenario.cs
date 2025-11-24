using CodeBeaker.AI.Agent.Services;
using Serilog;

namespace CodeBeaker.AI.Agent.Scenarios;

/// <summary>
/// Test-Driven Development scenario: Generate tests → Fail → Implement → Improve until pass
/// Phase 13: Debug & Improvement
/// </summary>
public class TestDrivenScenario
{
    private readonly OpenAIService _ai;
    private readonly CodeBeakerClient _codebeaker;
    private const int MaxIterations = 5;

    public TestDrivenScenario(OpenAIService ai, CodeBeakerClient codebeaker)
    {
        _ai = ai;
        _codebeaker = codebeaker;
    }

    public async Task<bool> RunAsync(string taskDescription, string language = "python")
    {
        Log.Information("=== Test-Driven Development Scenario ===");
        Log.Information("Task: {Task}", taskDescription);

        string? sessionId = null;

        try
        {
            // Step 1: Create session
            Log.Information("[Step 1/6] Creating session...");
            var sessionResponse = await _codebeaker.SendRequestAsync<Dictionary<string, object>>(
                "session.create",
                new { language = language, runtimePreference = "Speed" });

            sessionId = sessionResponse?["sessionId"]?.ToString();
            if (string.IsNullOrEmpty(sessionId))
            {
                Log.Error("Failed to create session");
                return false;
            }

            Log.Information("Session created: {SessionId}", sessionId);

            // Step 2: Generate tests first (TDD approach)
            Log.Information("[Step 2/6] Generating tests...");
            var testCode = await _ai.GenerateTestsAsync(taskDescription, language);

            Log.Information("Generated tests:");
            Log.Information(testCode);

            // Write test file
            await _codebeaker.SendRequestAsync<object>(
                "session.execute",
                new
                {
                    sessionId = sessionId,
                    command = new
                    {
                        type = "write_file",
                        path = "test_implementation.py",
                        content = testCode
                    }
                });

            // Step 3: Run tests (expect failures)
            Log.Information("[Step 3/6] Running tests (expecting failures)...");
            var testResult = await ExecuteTests(sessionId, "test_implementation.py");

            if (testResult.success)
            {
                Log.Warning("Tests passed without implementation. This might indicate overly simple tests.");
                return true;
            }

            Log.Information("Tests failed as expected:");
            Log.Error(testResult.error);

            // Step 4: Generate initial implementation
            Log.Information("[Step 4/6] Generating initial implementation...");
            var implementation = await _ai.GenerateImplementationAsync(taskDescription, testCode, language);

            Log.Information("Generated implementation:");
            Log.Information(implementation);

            // Write implementation file
            await _codebeaker.SendRequestAsync<object>(
                "session.execute",
                new
                {
                    sessionId = sessionId,
                    command = new
                    {
                        type = "write_file",
                        path = "implementation.py",
                        content = implementation
                    }
                });

            // Step 5: Iterative improvement until tests pass
            Log.Information("[Step 5/6] Iterative improvement cycle...");
            int iteration = 0;
            bool allTestsPass = false;

            while (iteration < MaxIterations && !allTestsPass)
            {
                iteration++;
                Log.Information("--- Iteration {Iteration}/{Max} ---", iteration, MaxIterations);

                // Combine implementation with tests and run
                var combinedCode = $"{implementation}\n\n# Run tests\n{testCode}";

                await _codebeaker.SendRequestAsync<object>(
                    "session.execute",
                    new
                    {
                        sessionId = sessionId,
                        command = new
                        {
                            type = "write_file",
                            path = "combined.py",
                            content = combinedCode
                        }
                    });

                var execResponse = await _codebeaker.SendRequestAsync<Dictionary<string, object>>(
                    "session.execute",
                    new
                    {
                        sessionId = sessionId,
                        command = new
                        {
                            type = "execute",
                            code = "exec(open('combined.py').read())"
                        }
                    });

                var success = execResponse?["success"]?.ToString()?.ToLower() == "true";
                var output = execResponse?["output"]?.ToString() ?? "";
                var error = execResponse?["error"]?.ToString() ?? "";

                if (success)
                {
                    Log.Information("✓ All tests passed!");
                    Log.Information("Output:");
                    Log.Information(output);
                    allTestsPass = true;
                }
                else
                {
                    Log.Warning($"✗ Tests failed (Iteration {iteration}):");
                    Log.Error(error);

                    if (iteration < MaxIterations)
                    {
                        Log.Information("Improving implementation...");
                        implementation = await _ai.ImproveImplementationAsync(implementation, error, language);

                        Log.Information("Improved implementation:");
                        Log.Information(implementation);

                        // Update implementation file
                        await _codebeaker.SendRequestAsync<object>(
                            "session.execute",
                            new
                            {
                                sessionId = sessionId,
                                command = new
                                {
                                    type = "write_file",
                                    path = "implementation.py",
                                    content = implementation
                                }
                            });
                    }
                }
            }

            // Step 6: Final summary
            Log.Information("[Step 6/6] Summary");
            if (allTestsPass)
            {
                Log.Information("✓ TDD cycle completed successfully!");
                Log.Information($"Tests passed after {iteration} iteration(s)");
                return true;
            }
            else
            {
                Log.Warning($"✗ Tests still failing after {MaxIterations} iterations");
                Log.Warning("Consider revising the task description or tests");
                return false;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Test-Driven Development scenario failed");
            return false;
        }
        finally
        {
            // Cleanup: Close session
            if (!string.IsNullOrEmpty(sessionId))
            {
                try
                {
                    await _codebeaker.SendRequestAsync<object>(
                        "session.close",
                        new { sessionId = sessionId });
                    Log.Information("Session closed: {SessionId}", sessionId);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to close session");
                }
            }
        }
    }

    private async Task<(bool success, string output, string error)> ExecuteTests(string sessionId, string testFile)
    {
        try
        {
            var execResponse = await _codebeaker.SendRequestAsync<Dictionary<string, object>>(
                "session.execute",
                new
                {
                    sessionId = sessionId,
                    command = new
                    {
                        type = "execute",
                        code = $"exec(open('{testFile}').read())"
                    }
                });

            var success = execResponse?["success"]?.ToString()?.ToLower() == "true";
            var output = execResponse?["output"]?.ToString() ?? "";
            var error = execResponse?["error"]?.ToString() ?? "";

            return (success, output, error);
        }
        catch (Exception ex)
        {
            return (false, "", ex.Message);
        }
    }
}
