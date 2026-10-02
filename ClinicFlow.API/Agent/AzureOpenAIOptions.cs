namespace ClinicFlow.Api.Agent
{
    public class AzureOpenAIOptions
    {
        public const string SectionName = "AzureOpenAI";

        public string Endpoint { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string DeploymentName { get; set; } = string.Empty;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(Endpoint)
            && !string.IsNullOrWhiteSpace(ApiKey)
            && !string.IsNullOrWhiteSpace(DeploymentName);
    }
}
