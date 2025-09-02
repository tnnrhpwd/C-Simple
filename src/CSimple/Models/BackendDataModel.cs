global using GlobalDataModel = DataModel;
global using GlobalDataItem = DataItem;
global using GlobalDataObject = DataObject;
using System.Text.Json.Serialization;
using CSimple; // Add reference to CSimple namespace to access ActionFile

/// <summary>
/// Model that matches the actual backend response format
/// where 'data' is a pipe-delimited string, not an object
/// </summary>
public class BackendDataResponse
{
    [JsonPropertyName("data")]
    public List<BackendDataItem> Data { get; set; } = new List<BackendDataItem>();

    [JsonPropertyName("dataIsError")]
    public bool DataIsError { get; set; }

    [JsonPropertyName("dataIsSuccess")]
    public bool DataIsSuccess { get; set; }

    [JsonPropertyName("dataIsLoading")]
    public bool DataIsLoading { get; set; }

    [JsonPropertyName("dataMessage")]
    public string DataMessage { get; set; } = string.Empty;

    [JsonPropertyName("operation")]
    public string Operation { get; set; } = string.Empty;
}

public class BackendDataItem
{
    [JsonPropertyName("data")]
    public string Data { get; set; } = string.Empty;  // This is a pipe-delimited string from backend

    [JsonPropertyName("files")]
    public List<object> Files { get; set; } = new List<object>();

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("__v")]
    public int? Version { get; set; }

    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;
}

/// <summary>
/// Helper class to parse pipe-delimited data strings from the backend
/// Format: "Creator:id|Plan:text|Goal:text|Action:text|Public:true|Agrees:ids"
/// </summary>
public static class BackendDataParser
{
    public static GlobalDataItem ConvertToDataItem(BackendDataItem backendItem)
    {
        var dataItem = new GlobalDataItem  // Use global DataItem
        {
            _id = backendItem.Id,
            updatedAt = backendItem.UpdatedAt,
            createdAt = backendItem.CreatedAt,
            __v = backendItem.Version ?? 0
        };

        // Parse the pipe-delimited data string
        var parsedData = ParsePipeDelimitedData(backendItem.Data);

        // Set the DataObject properties - preserve original pipe-delimited format
        dataItem.Data = new GlobalDataObject  // Use global DataObject
        {
            Text = backendItem.Data, // Use original pipe-delimited string from backend
            Files = new List<ActionFile>() // ActionFile should be accessible
        };

        // Set other properties
        dataItem.Creator = parsedData.GetValueOrDefault("Creator", "");
        dataItem.IsPublic = parsedData.GetValueOrDefault("Public", "false").ToLower() == "true";

        return dataItem;
    }

    private static Dictionary<string, string> ParsePipeDelimitedData(string data)
    {
        var result = new Dictionary<string, string>();

        if (string.IsNullOrEmpty(data))
            return result;

        var pairs = data.Split('|');
        foreach (var pair in pairs)
        {
            var colonIndex = pair.IndexOf(':');
            if (colonIndex > 0 && colonIndex < pair.Length - 1)
            {
                var key = pair.Substring(0, colonIndex);
                var value = pair.Substring(colonIndex + 1);
                result[key] = value;
            }
        }

        return result;
    }

    /// <summary>
    /// Convert BackendDataResponse to the expected DataModel format
    /// </summary>
    public static GlobalDataModel ConvertToDataModel(BackendDataResponse backendResponse)
    {
        return new GlobalDataModel  // Use the global DataModel class explicitly
        {
            Data = backendResponse.Data.Select(ConvertToDataItem).ToList(),
            DataIsError = backendResponse.DataIsError,
            DataIsSuccess = backendResponse.DataIsSuccess,
            DataIsLoading = backendResponse.DataIsLoading,
            DataMessage = backendResponse.DataMessage,
            Operation = backendResponse.Operation
        };
    }
}
