namespace Flexispace.Web.Services.Real;

// Bound from the "FlexiSpaceApi" section of appsettings.json.
public class FlexiSpaceApiOptions
{
    public const string SectionName = "FlexiSpaceApi";

    // e.g. "https://localhost:7055" - the API's own launch profile.
    // No trailing slash.
    public string BaseUrl { get; set; } = string.Empty;

    // e.g. "api://77163347-59be-48f4-8675-535af30a3a53/access_as_user" -
    // the same scope the TestClient already uses (FlexiSpace.TestClient/
    // src/authConfig.js). Reused as-is so Web doesn't need its own scope
    // exposed - it's asking for access to the same API.
    public string Scope { get; set; } = string.Empty;
}
