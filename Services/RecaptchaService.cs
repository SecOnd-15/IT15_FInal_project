using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Latog_Final_project.Services
{
    public interface IRecaptchaService
    {
        Task<bool> VerifyAsync(string response);
    }

    public class RecaptchaService : IRecaptchaService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public RecaptchaService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<bool> VerifyAsync(string response)
        {
            if (string.IsNullOrEmpty(response))
            {
                return false;
            }

            var secretKey = _configuration["Recaptcha:SecretKey"];
            if (string.IsNullOrEmpty(secretKey))
            {
                // Fallback to Google's public testing secret key if not configured
                secretKey = "6LeIxAcTAAAAAGG-vFI1TnRWxMZNFzsIM4w_7jNQ";
            }

            var postData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("secret", secretKey),
                new KeyValuePair<string, string>("response", response)
            });

            try
            {
                var res = await _httpClient.PostAsync("https://www.google.com/recaptcha/api/siteverify", postData);
                if (!res.IsSuccessStatusCode)
                {
                    return false;
                }

                var jsonString = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonString);
                if (doc.RootElement.TryGetProperty("success", out var successProp))
                {
                    return successProp.GetBoolean();
                }
            }
            catch
            {
                // Log or fail silently, default to unauthorized for security
            }

            return false;
        }
    }
}
