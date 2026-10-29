using System.Text.Json.Serialization;

namespace LeadManagement.Desktop.Models
{
    /// <summary>
    /// Represents the full API response envelope from the backend.
    /// </summary>
    public class ApiResponse<T>
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public T? Data { get; set; }
    }

    /// <summary>
    /// The data payload returned on a successful login.
    /// </summary>
    public class LoginData
    {
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;

        [JsonPropertyName("user")]
        public UserInfo User { get; set; } = new();
    }

    /// <summary>
    /// Basic user profile info returned from the API.
    /// </summary>
    public class UserInfo
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;

        [JsonPropertyName("full_name")]
        public string FullName { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;
    }
}
