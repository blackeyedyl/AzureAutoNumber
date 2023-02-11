using Newtonsoft.Json;

namespace AutoNumber.Documents;

public class AutoNumberState
{
    /// <summary>
    /// Partition key
    /// </summary>
    [JsonProperty("pk")]
    public string Pk => Id;

    /// <summary>
    /// Identifier
    /// </summary>
    [JsonProperty("id")]
    public string Id { get; set; }

    /// <summary>
    /// The next available number
    /// </summary>
    public long NextAvailableNumber { get; set; }

    /// <summary>
    /// Etag
    /// </summary>
    [JsonProperty("_etag")]
    public string ETag { get; private set; }
}