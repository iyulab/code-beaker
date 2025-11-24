using OpenAI.Chat;

namespace CodeBeaker.AI.Agent.Services;

/// <summary>
/// OpenAI API Service Wrapper
/// </summary>
public class OpenAIService
{
    private readonly ChatClient _chatClient;

    public OpenAIService(string apiKey, string model)
    {
        _chatClient = new ChatClient(model, apiKey);
    }

    /// <summary>
    /// Generate code based on task description
    /// </summary>
    public async Task<string> GenerateCodeAsync(string taskDescription, string language = "python")
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage($"You are an expert {language} programmer. Generate clean, working code without explanations. Only output code."),
            new UserChatMessage(taskDescription)
        };

        var response = await _chatClient.CompleteChatAsync(messages);
        var content = response.Value.Content[0].Text;

        // Remove markdown code blocks if present
        content = content.Trim();
        if (content.StartsWith("```"))
        {
            var lines = content.Split('\n');
            content = string.Join('\n', lines.Skip(1).SkipLast(1));
        }

        return content.Trim();
    }

    /// <summary>
    /// Analyze error and suggest fix
    /// </summary>
    public async Task<string> AnalyzeErrorAndFixAsync(string code, string error, string language = "python")
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage($"You are an expert {language} debugger. Analyze the error and provide fixed code. Only output the corrected code without explanations."),
            new UserChatMessage($"Original code:\n```{language}\n{code}\n```\n\nError:\n{error}\n\nProvide the fixed code:")
        };

        var response = await _chatClient.CompleteChatAsync(messages);
        var content = response.Value.Content[0].Text;

        // Remove markdown code blocks
        content = content.Trim();
        if (content.StartsWith("```"))
        {
            var lines = content.Split('\n');
            content = string.Join('\n', lines.Skip(1).SkipLast(1));
        }

        return content.Trim();
    }

    /// <summary>
    /// Analyze error and generate fix with unified diff
    /// </summary>
    public async Task<string> AnalyzeErrorAndGenerateDiffAsync(string code, string error, string language = "python")
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage($"You are an expert {language} debugger. Analyze the error and provide:\n1. Fixed code in a ```{language} block\n2. Unified diff in a ```diff block (optional)\n\nBe concise and clear."),
            new UserChatMessage($"Original code:\n```{language}\n{code}\n```\n\nError:\n{error}\n\nProvide the fixed code and optionally a unified diff:")
        };

        var response = await _chatClient.CompleteChatAsync(messages);
        return response.Value.Content[0].Text;
    }

    /// <summary>
    /// Generate test cases
    /// </summary>
    public async Task<string> GenerateTestsAsync(string functionDescription, string language = "python")
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage($"You are an expert {language} test writer. Generate comprehensive test cases. Only output test code."),
            new UserChatMessage($"Generate test cases for: {functionDescription}")
        };

        var response = await _chatClient.CompleteChatAsync(messages);
        var content = response.Value.Content[0].Text;

        content = content.Trim();
        if (content.StartsWith("```"))
        {
            var lines = content.Split('\n');
            content = string.Join('\n', lines.Skip(1).SkipLast(1));
        }

        return content.Trim();
    }

    /// <summary>
    /// Generate implementation from test description
    /// </summary>
    public async Task<string> GenerateImplementationAsync(string taskDescription, string testCode, string language = "python")
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage($"You are an expert {language} programmer. Generate implementation that passes the given tests. Only output code."),
            new UserChatMessage($"Task: {taskDescription}\n\nTests:\n```{language}\n{testCode}\n```\n\nProvide implementation:")
        };

        var response = await _chatClient.CompleteChatAsync(messages);
        var content = response.Value.Content[0].Text;

        content = content.Trim();
        if (content.StartsWith("```"))
        {
            var lines = content.Split('\n');
            content = string.Join('\n', lines.Skip(1).SkipLast(1));
        }

        return content.Trim();
    }

    /// <summary>
    /// Improve implementation based on test failures
    /// </summary>
    public async Task<string> ImproveImplementationAsync(string code, string failures, string language = "python")
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage($"You are an expert {language} programmer. Improve the code to fix test failures. Only output improved code."),
            new UserChatMessage($"Current code:\n```{language}\n{code}\n```\n\nTest failures:\n{failures}\n\nProvide improved code:")
        };

        var response = await _chatClient.CompleteChatAsync(messages);
        var content = response.Value.Content[0].Text;

        content = content.Trim();
        if (content.StartsWith("```"))
        {
            var lines = content.Split('\n');
            content = string.Join('\n', lines.Skip(1).SkipLast(1));
        }

        return content.Trim();
    }
}
