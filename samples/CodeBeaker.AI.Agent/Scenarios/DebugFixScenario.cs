using CodeBeaker.AI.Agent.Services;
using Serilog;

namespace CodeBeaker.AI.Agent.Scenarios;

/// <summary>
/// Debug & Fix scenario: Execute buggy code → Analyze error → Generate fix → Apply patch → Verify
/// Phase 13: Debug & Improvement
/// </summary>
public class DebugFixScenario
{
    private readonly OpenAIService _ai;
    private readonly CodeBeakerClient _codebeaker;

    public DebugFixScenario(OpenAIService ai, CodeBeakerClient codebeaker)
    {
        _ai = ai;
        _codebeaker = codebeaker;
    }

    public async Task<bool> RunAsync(string buggyFilePath, string language = "python")
    {
        Log.Information("=== Debug & Fix Scenario ===");
        Log.Information("Buggy file: {FilePath}", buggyFilePath);

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

            // Step 2: Read buggy code
            Log.Information("[Step 2/6] Reading buggy code...");
            if (!File.Exists(buggyFilePath))
            {
                Log.Error("Buggy file not found: {FilePath}", buggyFilePath);
                return false;
            }

            var buggyCode = await File.ReadAllTextAsync(buggyFilePath);
            Log.Information("Buggy code loaded ({Length} bytes)", buggyCode.Length);

            // Step 3: Execute buggy code and capture error
            Log.Information("[Step 3/6] Executing buggy code...");
            var fileName = Path.GetFileName(buggyFilePath);

            // Write file to session
            await _codebeaker.SendRequestAsync<object>(
                "session.execute",
                new
                {
                    sessionId = sessionId,
                    command = new
                    {
                        type = "write_file",
                        path = fileName,
                        content = buggyCode
                    }
                });

            // Execute and capture error
            var execResponse = await _codebeaker.SendRequestAsync<Dictionary<string, object>>(
                "session.execute",
                new
                {
                    sessionId = sessionId,
                    command = new
                    {
                        type = "execute",
                        code = $"exec(open('{fileName}').read())"
                    }
                });

            // Extract result from response (contains stdout, stderr, exitCode)
            var resultJson = System.Text.Json.JsonSerializer.Serialize(execResponse?["result"]);
            var resultObj = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(resultJson);

            var stdout = resultObj?["stdout"]?.ToString() ?? "";
            var stderr = resultObj?["stderr"]?.ToString() ?? "";
            var exitCodeStr = resultObj?["exitCode"]?.ToString() ?? "0";
            int.TryParse(exitCodeStr, out var exitCode);

            // Check if execution failed (non-zero exit code or stderr contains error)
            var hasError = exitCode != 0 || !string.IsNullOrEmpty(stderr);

            if (!hasError)
            {
                Log.Warning("Code executed successfully (no bug detected). Output:");
                Log.Warning(stdout);
                return true; // Not a failure, just no bug found
            }

            Log.Information("Error detected:");
            Log.Error(stderr);

            // Step 4: AI analyzes error and generates fix with diff
            Log.Information("[Step 4/6] AI analyzing error and generating fix...");
            var fixedCodeWithDiff = await _ai.AnalyzeErrorAndGenerateDiffAsync(buggyCode, stderr, language);

            if (string.IsNullOrEmpty(fixedCodeWithDiff))
            {
                Log.Error("AI failed to generate fix");
                return false;
            }

            // Parse the response to extract fixed code and diff
            var (fixedCode, diff) = ParseFixResponse(fixedCodeWithDiff);

            Log.Information("AI generated fix:");
            Log.Information("--- Fixed Code ---");
            Log.Information(fixedCode);

            if (!string.IsNullOrEmpty(diff))
            {
                Log.Information("--- Unified Diff ---");
                Log.Information(diff);

                // Step 5: Apply patch (if diff available)
                Log.Information("[Step 5/6] Applying patch...");

                // For simulation, we'll use the fixed code directly
                // In production, you would use ApplyPatchCommand here
                await _codebeaker.SendRequestAsync<object>(
                    "session.execute",
                    new
                    {
                        sessionId = sessionId,
                        command = new
                        {
                            type = "write_file",
                            path = fileName,
                            content = fixedCode
                        }
                    });

                Log.Information("Patch applied successfully");
            }
            else
            {
                // No diff, just use fixed code
                await _codebeaker.SendRequestAsync<object>(
                    "session.execute",
                    new
                    {
                        sessionId = sessionId,
                        command = new
                        {
                            type = "write_file",
                            path = fileName,
                            content = fixedCode
                        }
                    });
            }

            // Step 6: Re-execute to verify fix
            Log.Information("[Step 6/6] Re-executing fixed code...");
            var verifyResponse = await _codebeaker.SendRequestAsync<Dictionary<string, object>>(
                "session.execute",
                new
                {
                    sessionId = sessionId,
                    command = new
                    {
                        type = "execute",
                        code = $"exec(open('{fileName}').read())"
                    }
                });

            var verifySuccess = verifyResponse?["success"]?.ToString()?.ToLower() == "true";
            var verifyOutput = verifyResponse?["output"]?.ToString() ?? "";
            var verifyError = verifyResponse?["error"]?.ToString() ?? "";

            if (verifySuccess)
            {
                Log.Information("✓ Fix verified! Code now runs successfully.");
                Log.Information("Output:");
                Log.Information(verifyOutput);
                return true;
            }
            else
            {
                Log.Error("✗ Fix failed. Still has errors:");
                Log.Error(verifyError);
                return false;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Debug & Fix scenario failed");
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

    private (string fixedCode, string diff) ParseFixResponse(string response)
    {
        // Try to extract code blocks and diff from AI response
        var lines = response.Split('\n');
        var fixedCode = new List<string>();
        var diff = new List<string>();

        bool inCodeBlock = false;
        bool inDiffBlock = false;
        string blockType = "";

        foreach (var line in lines)
        {
            if (line.StartsWith("```"))
            {
                if (!inCodeBlock && !inDiffBlock)
                {
                    // Start of block
                    inCodeBlock = !line.Contains("diff");
                    inDiffBlock = line.Contains("diff");
                    blockType = line.Replace("```", "").Trim();
                }
                else
                {
                    // End of block
                    inCodeBlock = false;
                    inDiffBlock = false;
                }
                continue;
            }

            if (inCodeBlock)
            {
                fixedCode.Add(line);
            }
            else if (inDiffBlock)
            {
                diff.Add(line);
            }
        }

        // If no markdown blocks found, treat entire response as code
        if (fixedCode.Count == 0 && diff.Count == 0)
        {
            return (response.Trim(), "");
        }

        return (string.Join('\n', fixedCode), string.Join('\n', diff));
    }
}
