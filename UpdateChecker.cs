using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HelloAutofill;

record UpdateResult(string Tag, bool IsNewer);

static class UpdateChecker
{
    private static readonly HttpClient Client = CreateClient();
    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("HelloAutofill/" + AppInfo.Version);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }
    private static Version ParseVersion(string text)
    {
        if (!Regex.IsMatch(text, @"^\d+\.\d+\.\d+$") || !Version.TryParse(text, out var version))
            throw new FormatException("Invalid release version.");
        return version;
    }
    public static async Task<UpdateResult> CheckAsync()
    {
        using var response = await Client.GetAsync("https://api.github.com/repos/udaradhananjaya/windows-password-manager/releases/latest");
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        string tag = json.RootElement.GetProperty("tag_name").GetString() ?? throw new FormatException("Release tag is missing.");
        if (!tag.StartsWith('v')) throw new FormatException("Invalid release tag.");
        return new UpdateResult(tag, ParseVersion(tag[1..]) > ParseVersion(AppInfo.Version));
    }
    public static bool IsCheckFailure(Exception ex) => ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException;
}
