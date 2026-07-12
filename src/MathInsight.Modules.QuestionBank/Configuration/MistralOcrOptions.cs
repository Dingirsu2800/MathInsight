namespace MathInsight.Modules.QuestionBank.Configuration;

public sealed class MistralOcrOptions
{
    public const string SectionName = "MistralOcr";

    public string? ApiKey { get; set; }
    public string BaseUrl { get; set; } = "https://api.mistral.ai";
    public string Model { get; set; } = "mistral-ocr-latest";
}
