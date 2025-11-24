using CodeBeaker.AI.Agent.Services;

namespace CodeBeaker.AI.Agent.Scenarios;

/// <summary>
/// Simple Coding Scenario: AI generates code and executes it
/// </summary>
public class SimpleCodingScenario
{
    private readonly OpenAIService _ai;
    private readonly CodeBeakerClient _codebeaker;

    public SimpleCodingScenario(OpenAIService ai, CodeBeakerClient codebeaker)
    {
        _ai = ai;
        _codebeaker = codebeaker;
    }

    public async Task<bool> RunAsync(string taskDescription, string language = "python")
    {
        Console.WriteLine($"\n[Scenario] Simple Coding: {taskDescription}");
        Console.WriteLine(new string('=', 70));

        try
        {
            // Step 1: Create CodeBeaker session
            Console.WriteLine("\n[Step 1] Creating CodeBeaker session...");
            var sessionResponse = await _codebeaker.SendRequestAsync<Dictionary<string, object>>(
                "session.create",
                new
                {
                    language = language,
                    runtimePreference = "Speed"
                });

            var sessionId = sessionResponse?["sessionId"]?.ToString();
            if (string.IsNullOrEmpty(sessionId))
            {
                throw new Exception("Failed to create session");
            }
            Console.WriteLine($"✅ Session created: {sessionId}");

            // Step 2: Generate code using AI
            Console.WriteLine("\n[Step 2] Requesting code from OpenAI...");
            var code = await _ai.GenerateCodeAsync(taskDescription, language);
            Console.WriteLine($"✅ Code generated ({code.Length} characters)");
            Console.WriteLine("\n--- Generated Code ---");
            Console.WriteLine(code);
            Console.WriteLine("--- End Code ---\n");

            // Step 3: Write code to file
            Console.WriteLine("[Step 3] Writing code to CodeBeaker workspace...");
            var fileName = language == "python" ? "solution.py" : "solution.js";
            await _codebeaker.SendRequestAsync<object>(
                "session.execute",
                new
                {
                    sessionId = sessionId,
                    command = new
                    {
                        type = "write_file",
                        path = fileName,
                        content = code
                    }
                });
            Console.WriteLine($"✅ File written: {fileName}");

            // Step 4: Execute code
            Console.WriteLine("\n[Step 4] Executing code...");
            var execResponse = await _codebeaker.SendRequestAsync<Dictionary<string, object>>(
                "session.execute",
                new
                {
                    sessionId = sessionId,
                    command = new
                    {
                        type = "execute",
                        code = language == "python" ? $"exec(open('{fileName}').read())" : $"require('./{fileName}')"
                    }
                });

            var success = execResponse?["success"]?.ToString() == "True";
            var result = execResponse?["result"]?.ToString() ?? "";
            var error = execResponse?["error"]?.ToString() ?? "";

            if (success)
            {
                Console.WriteLine("✅ Execution successful!");
                Console.WriteLine("\n--- Output ---");
                Console.WriteLine(result);
                Console.WriteLine("--- End Output ---");
            }
            else
            {
                Console.WriteLine("❌ Execution failed!");
                Console.WriteLine($"Error: {error}");
                return false;
            }

            // Step 5: Close session
            Console.WriteLine("\n[Step 5] Closing session...");
            await _codebeaker.SendRequestAsync<object>("session.close", new { sessionId });
            Console.WriteLine("✅ Session closed");

            Console.WriteLine($"\n{new string('=', 70)}");
            Console.WriteLine("✅ Scenario completed successfully!");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Scenario failed: {ex.Message}");
            return false;
        }
    }
}
