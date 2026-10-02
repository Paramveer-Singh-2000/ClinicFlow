using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;

namespace ClinicFlow.API.Agent
{
    public record ChatTurn(string Role, string Content);

    public record ChatRequest(string Message, List<ChatTurn>? History);

    public record ChatResponse(string Reply, List<ChatTurn> History);
    public class ChatService
    {
        private readonly Kernel _kernel;
        private readonly IChatCompletionService _chat;

        private const int MaxHistoryTurns = 20;

        public ChatService(Kernel kernel)
        {
            _kernel = kernel;
            _chat = kernel.GetRequiredService<IChatCompletionService>();
        }

        public async Task<ChatResponse> SendAsync(ChatRequest request, CancellationToken ct = default)
        {
            var history = new ChatHistory(AgentPrompts.SystemPrompt);

            foreach (var turn in (request.History ?? []).TakeLast(MaxHistoryTurns))
            {
                if (turn.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                    history.AddAssistantMessage(turn.Content);
                else
                    history.AddUserMessage(turn.Content);
            }

            history.AddUserMessage(request.Message);

            var settings = new AzureOpenAIPromptExecutionSettings
            {
                // Auto() runs the whole loop: the model requests a tool, Semantic
                // Kernel invokes it, feeds the result back, and repeats until the
                // model produces a plain text reply. Multi-step bookings need this.
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                Temperature = 0.3,
                MaxTokens = 500
            };

            var result = await _chat.GetChatMessageContentAsync(history, settings, _kernel, ct);

            var reply = result.Content ?? "Sorry, I didn't catch that. Could you say it again?";

            // Only user and assistant text goes back to the client. Tool calls stay
            // server-side — the patient never sees the machinery.
            var updated = new List<ChatTurn>(request.History ?? [])
        {
            new("user", request.Message),
            new("assistant", reply)
        };

            return new ChatResponse(reply, updated.TakeLast(MaxHistoryTurns).ToList());
        }


    }
}
