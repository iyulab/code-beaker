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
}
