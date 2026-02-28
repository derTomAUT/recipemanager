using RecipeManager.Api.Services;
using Xunit;

public class AiResponseParserTests
{
    [Fact]
    public void ExtractOpenAiMessageContent_HandlesArrayTextParts()
    {
        var responseBody = """
        {
          "choices": [
            {
              "message": {
                "content": [
                  { "type": "output_text", "text": "Here are suggestions" },
                  { "type": "output_text", "text": "{\"suggestions\":[{\"recipeId\":\"11111111-1111-1111-1111-111111111111\",\"reason\":\"cozy\"}]}" }
                ]
              }
            }
          ]
        }
        """;

        var content = AiResponseParser.ExtractOpenAiMessageContent(responseBody);

        Assert.NotNull(content);
        Assert.Contains("\"suggestions\"", content);
    }

    [Fact]
    public void ExtractJsonObjectText_HandlesCodeFence()
    {
        var content = """
        ```json
        {"title":"Soup","ingredients":[]}
        ```
        """;

        var json = AiResponseParser.ExtractJsonObjectText(content);

        Assert.Equal("{\"title\":\"Soup\",\"ingredients\":[]}", json);
    }

    [Fact]
    public void ExtractJsonObjectText_HandlesEmbeddedObject()
    {
        var content = "Result:\n{\"title\":\"Stew\",\"steps\":[]}\nDone.";

        var json = AiResponseParser.ExtractJsonObjectText(content);

        Assert.Equal("{\"title\":\"Stew\",\"steps\":[]}", json);
    }
}
