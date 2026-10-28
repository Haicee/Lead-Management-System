using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using LeadManagement.Desktop.Models;

namespace LeadManagement.Desktop.Services
{
    /// <summary>
    /// Centralized HTTP client for communicating with the PHP REST API backend.
    /// All API calls go through this service.
    /// </summary>
    public class ApiService
    {
        private static readonly Lazy<ApiService> _instance = new(() => new ApiService());
        public static ApiService Instance => _instance.Value;

        private readonly HttpClient _httpClient;
        private const string BaseUrl = "http://localhost/lms-api";

        /// <summary>
        /// The auth token received after a successful login.
        /// </summary>
        public string? AuthToken { get; private set; }

        /// <summary>
        /// The currently logged-in user info.
        /// </summary>
        public UserInfo? CurrentUser { get; private set; }

        private ApiService()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(BaseUrl + "/"),
                Timeout = TimeSpan.FromSeconds(15)
            };
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        }

        /// <summary>
        /// Attempts to log in with the given username and password.
        /// Returns (success, message) tuple.
        /// </summary>
        public async Task<(bool Success, string Message)> LoginAsync(string username, string password)
        {
            try
            {
                var payload = new { username, password };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("auth/login", content);
                var responseBody = await response.Content.ReadAsStringAsync();

                var result = JsonSerializer.Deserialize<ApiResponse<LoginData>>(responseBody);

                if (result == null)
                {
                    return (false, "Unable to parse server response.");
                }

                if (result.Success && result.Data != null)
                {
                    AuthToken = result.Data.Token;
                    CurrentUser = result.Data.User;

                    // Set the token for future requests
                    _httpClient.DefaultRequestHeaders.Remove("Authorization");
                    _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {AuthToken}");

                    return (true, result.Message);
                }

                return (false, result.Message);
            }
            catch (HttpRequestException)
            {
                return (false, "Cannot connect to the server. Please ensure XAMPP Apache is running.");
            }
            catch (TaskCanceledException)
            {
                return (false, "Connection timed out. Please check your network and server status.");
            }
            catch (Exception ex)
            {
                return (false, $"An unexpected error occurred: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends a batch of lead records to the backend database via POST /leads/import.
        /// </summary>
        public async Task<(bool Success, string Message, ImportResultData? Data)> ImportLeadsAsync(
            string batchName, 
            string fileName, 
            string initialStatus, 
            System.Collections.Generic.IEnumerable<LeadModel> leads)
        {
            try
            {
                var payload = new
                {
                    batch_name = batchName,
                    file_name = fileName,
                    initial_status = initialStatus,
                    user_id = CurrentUser?.Id,
                    leads = leads
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("leads/import", content);
                var responseBody = await response.Content.ReadAsStringAsync();

                var result = JsonSerializer.Deserialize<ApiResponse<ImportResultData>>(responseBody);

                if (result == null)
                {
                    return (false, "Unable to parse server response.", null);
                }

                return (result.Success, result.Message, result.Data);
            }
            catch (HttpRequestException)
            {
                return (false, "Cannot connect to server. Please ensure XAMPP Apache & MySQL are running.", null);
            }
            catch (TaskCanceledException)
            {
                return (false, "Import request timed out. Large batches may need more time.", null);
            }
            catch (Exception ex)
            {
                return (false, $"Error importing leads: {ex.Message}", null);
            }
        }

        /// <summary>
        /// Fetches leads from the backend with optional search, status, and city filters.
        /// </summary>
        public async Task<(bool Success, string Message, LeadListData? Data)> GetLeadsAsync(
            string? search = null,
            string? status = null,
            string? city = null,
            int limit = 5000,
            int offset = 0)
        {
            try
            {
                var queryParams = new System.Collections.Generic.List<string>();
                if (limit > 0) queryParams.Add($"limit={limit}");
                if (offset > 0) queryParams.Add($"offset={offset}");
                if (!string.IsNullOrWhiteSpace(search)) queryParams.Add($"search={Uri.EscapeDataString(search)}");
                if (!string.IsNullOrWhiteSpace(status) && status != "All Statuses") queryParams.Add($"status={Uri.EscapeDataString(status)}");
                if (!string.IsNullOrWhiteSpace(city) && city != "All Cities") queryParams.Add($"city={Uri.EscapeDataString(city)}");

                var url = "leads" + (queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "");
                var response = await _httpClient.GetAsync(url);
                var responseBody = await response.Content.ReadAsStringAsync();

                var result = JsonSerializer.Deserialize<ApiResponse<LeadListData>>(responseBody);

                if (result == null)
                {
                    return (false, "Unable to parse server response.", null);
                }

                return (result.Success, result.Message, result.Data);
            }
            catch (HttpRequestException)
            {
                return (false, "Cannot connect to server. Please ensure XAMPP Apache & MySQL are running.", null);
            }
            catch (Exception ex)
            {
                return (false, $"Error fetching leads: {ex.Message}", null);
            }
        }

        /// <summary>
        /// Fetches distinct cities and statuses for masterlist filters.
        /// </summary>
        public async Task<LeadFiltersData?> GetLeadFiltersAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("leads/filters");
                var responseBody = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ApiResponse<LeadFiltersData>>(responseBody);
                return result?.Data;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Fetches all import batches for the Import History page.
        /// </summary>
        public async Task<(bool Success, string Message, System.Collections.Generic.List<ImportBatchModel>? Batches)> GetBatchesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("batches");
                var responseBody = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ApiResponse<System.Collections.Generic.List<ImportBatchModel>>>(responseBody);

                if (result == null)
                {
                    return (false, "Unable to parse server response.", null);
                }

                return (result.Success, result.Message, result.Data);
            }
            catch (HttpRequestException)
            {
                return (false, "Cannot connect to server. Please ensure XAMPP Apache & MySQL are running.", null);
            }
            catch (Exception ex)
            {
                return (false, $"Error fetching import batches: {ex.Message}", null);
            }
        }

        /// <summary>
        /// Fetches all leads belonging to a specific batch (for preview or specific batch export).
        /// </summary>
        public async Task<(bool Success, string Message, System.Collections.Generic.List<LeadModel>? Leads)> GetBatchLeadsAsync(int batchId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"batches/{batchId}/leads");
                var responseBody = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ApiResponse<LeadListData>>(responseBody);

                if (result == null)
                {
                    return (false, "Unable to parse server response.", null);
                }

                return (result.Success, result.Message, result.Data?.Leads);
            }
            catch (HttpRequestException)
            {
                return (false, "Cannot connect to server. Please ensure XAMPP Apache & MySQL are running.", null);
            }
            catch (Exception ex)
            {
                return (false, $"Error fetching batch leads: {ex.Message}", null);
            }
        }

        /// <summary>
        /// Deletes a batch and all associated leads permanently from the database.
        /// </summary>
        public async Task<(bool Success, string Message)> DeleteBatchAsync(int batchId)
        {
            try
            {
                var payload = new { id = batchId };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("batches/delete", content);
                var responseBody = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ApiResponse<object>>(responseBody);

                if (result == null)
                {
                    return (false, "Unable to parse server response.");
                }

                return (result.Success, result.Message);
            }
            catch (HttpRequestException)
            {
                return (false, "Cannot connect to server. Please ensure XAMPP Apache & MySQL are running.");
            }
            catch (Exception ex)
            {
                return (false, $"Error deleting batch: {ex.Message}");
            }
        }

        /// <summary>
        /// Clears the stored auth state on logout.
        /// </summary>
        public void Logout()
        {
            AuthToken = null;
            CurrentUser = null;
            _httpClient.DefaultRequestHeaders.Remove("Authorization");
        }
    }
}
