using CodeBeaker.AI.Agent.Services;
using CodeBeaker.AI.Agent.Scenarios;
using DotNetEnv;

namespace CodeBeaker.AI.Agent;

class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║         CodeBeaker AI Agent - Demo Sample                ║");
        Console.WriteLine("║         Phase 12: AI Agent Integration                   ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

        try
        {
            // Load .env file from project root
            var projectRoot = FindProjectRoot();
            if (projectRoot != null)
            {
                var envPath = Path.Combine(projectRoot, ".env");
                if (File.Exists(envPath))
                {
                    Env.Load(envPath);
                    Console.WriteLine($"✅ Loaded .env from: {envPath}\n");
                }
                else
                {
                    Console.WriteLine($"⚠️  No .env file found at: {envPath}");
                    Console.WriteLine("Please create .env with OPENAI_API_KEY and OPENAI_MODEL\n");
                    return 1;
                }
            }

            // Get environment variables
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            var model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4";

            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("❌ Error: OPENAI_API_KEY not found in environment");
                Console.WriteLine("Please set OPENAI_API_KEY in .env file\n");
                return 1;
            }

            Console.WriteLine($"🤖 Using OpenAI Model: {model}");
            Console.WriteLine($"🔗 CodeBeaker API: ws://localhost:5039/ws/jsonrpc\n");

            // Initialize services
            var ai = new OpenAIService(apiKey, model);
            var codebeaker = new CodeBeakerClient();

            // Connect to CodeBeaker
            Console.WriteLine("Connecting to CodeBeaker...");
            await codebeaker.ConnectAsync();

            // Parse command line arguments
            if (args.Length == 0)
            {
                await RunDefaultScenarios(ai, codebeaker);
            }
            else
            {
                var scenarioType = args[0].ToLower();
                var task = args.Length > 1 ? string.Join(" ", args.Skip(1)) : GetDefaultTask(scenarioType);

                await RunScenario(scenarioType, task, ai, codebeaker);
            }

            codebeaker.Dispose();
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Fatal Error: {ex.Message}");
            Console.WriteLine($"\nStack Trace:\n{ex.StackTrace}");
            return 1;
        }
    }

    static async Task RunDefaultScenarios(OpenAIService ai, CodeBeakerClient codebeaker)
    {
        Console.WriteLine("Running default demonstration scenarios...\n");

        // Scenario 1: Simple Function
        var scenario1 = new SimpleCodingScenario(ai, codebeaker);
        await scenario1.RunAsync(
            "Write a Python function to calculate the factorial of a number, with example usage showing factorial(5) and factorial(10)",
            "python"
        );

        Console.WriteLine("\n" + new string('█', 70) + "\n");

        // Scenario 2: Data Processing
        var scenario2 = new SimpleCodingScenario(ai, codebeaker);
        await scenario2.RunAsync(
            "Write a Python function that takes a list of numbers and returns a dictionary with 'sum', 'average', 'min', and 'max'. Include example usage.",
            "python"
        );

        Console.WriteLine("\n" + new string('█', 70) + "\n");

        // Scenario 3: String Manipulation
        var scenario3 = new SimpleCodingScenario(ai, codebeaker);
        await scenario3.RunAsync(
            "Write a Python function that reverses words in a sentence (not the entire string). For example, 'Hello World' becomes 'olleH dlroW'. Show usage.",
            "python"
        );
    }

    static async Task RunScenario(string scenarioType, string task, OpenAIService ai, CodeBeakerClient codebeaker)
    {
        switch (scenarioType)
        {
            case "simple":
                var simple = new SimpleCodingScenario(ai, codebeaker);
                await simple.RunAsync(task);
                break;

            default:
                Console.WriteLine($"❌ Unknown scenario type: {scenarioType}");
                Console.WriteLine("\nAvailable scenarios:");
                Console.WriteLine("  simple <task>  - Simple coding task");
                break;
        }
    }

    static string GetDefaultTask(string scenarioType)
    {
        return scenarioType switch
        {
            "simple" => "Write a Python function to calculate Fibonacci sequence up to n terms",
            _ => "Write a Python hello world program"
        };
    }

    static string? FindProjectRoot()
    {
        var current = Directory.GetCurrentDirectory();

        while (current != null)
        {
            if (File.Exists(Path.Combine(current, ".env")))
            {
                return current;
            }

            var parent = Directory.GetParent(current);
            current = parent?.FullName;
        }

        return null;
    }
}
