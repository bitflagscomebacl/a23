using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseRouting();
app.UseCors();
app.MapControllers();

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Run($"http://0.0.0.0:{port}");

public class LicenseRequest
{
    public string Key { get; set; } = "";
    public string HardwareId { get; set; } = "";
    public long Timestamp { get; set; }
}

public class LicenseResponse
{
    public bool IsValid { get; set; }
    public string Message { get; set; } = "";
}

public class LicenseData
{
    public string Key { get; set; } = "";
    public string HardwareId { get; set; } = "";
    public bool IsActive { get; set; }
    public long CreatedAt { get; set; }
    public long ExpiresAt { get; set; }
}

public static class LicenseManager
{
    private static readonly HttpClient _httpClient = new HttpClient();
    private static readonly string _pastebinUrl = "https://pastebin.com/raw/HbUuGb6F"; // Replace with your pastebin raw URL
    private static List<string> _cachedKeys = new List<string>();
    private static DateTime _lastCacheUpdate = DateTime.MinValue;
    private static readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(5); // Cache for 5 minutes

    private static readonly Dictionary<string, LicenseData> _activeLicenses = new Dictionary<string, LicenseData>();

    public static async Task<bool> ValidateLicense(string key, string hardwareId)
    {
        // Update cache if needed
        if (DateTime.UtcNow - _lastCacheUpdate > _cacheExpiry)
        {
            await UpdateKeyCache();
        }

        // Check if key exists in pastebin
        if (!_cachedKeys.Contains(key))
        {
            return false;
        }

        // Check if license is already active
        if (_activeLicenses.TryGetValue(key, out var existingLicense))
        {
            // Check if expired
            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > existingLicense.ExpiresAt)
            {
                _activeLicenses.Remove(key);
                return false;
            }

            // Check if active
            if (!existingLicense.IsActive)
            {
                return false;
            }

            // Check hardware ID binding
            if (string.IsNullOrEmpty(existingLicense.HardwareId))
            {
                existingLicense.HardwareId = hardwareId;
                return true;
            }

            return existingLicense.HardwareId == hardwareId;
        }

        // Create new license entry
        var newLicense = new LicenseData
        {
            Key = key,
            HardwareId = hardwareId,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds()
        };

        _activeLicenses[key] = newLicense;
        return true;
    }

    private static async Task UpdateKeyCache()
    {
        try
        {
            var response = await _httpClient.GetStringAsync(_pastebinUrl);
            var keys = response.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                              .Select(k => k.Trim())
                              .Where(k => !string.IsNullOrWhiteSpace(k))
                              .ToList();

            _cachedKeys = keys;
            _lastCacheUpdate = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to update key cache: {ex.Message}");
            // Keep using cached keys if update fails
        }
    }

    public static async Task<List<string>> GetValidKeys()
    {
        if (DateTime.UtcNow - _lastCacheUpdate > _cacheExpiry)
        {
            await UpdateKeyCache();
        }
        return _cachedKeys.ToList();
    }

    public static void RevokeLicense(string key)
    {
        if (_activeLicenses.TryGetValue(key, out var license))
        {
            license.IsActive = false;
        }
    }

    public static void AddKeyToCache(string key)
    {
        if (!_cachedKeys.Contains(key))
        {
            _cachedKeys.Add(key);
        }
    }
}

[ApiController]
[Route("")]
public class LicenseController : ControllerBase
{
    [HttpPost("validate")]
    public async Task<IActionResult> ValidateLicense([FromBody] LicenseRequest request)
    {
        try
        {
            var isValid = await LicenseManager.ValidateLicense(request.Key, request.HardwareId);
            return Ok(new LicenseResponse
            {
                IsValid = isValid,
                Message = isValid ? "License valid" : "Invalid license key or expired"
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new LicenseResponse
            {
                IsValid = false,
                Message = ex.Message
            });
        }
    }

    [HttpPost("add-key")]
    public IActionResult AddKeyToCache([FromBody] AddKeyRequest request)
    {
        try
        {
            LicenseManager.AddKeyToCache(request.Key);
            return Ok(new { Message = "Key added to cache successfully" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    [HttpPost("revoke")]
    public IActionResult RevokeLicense([FromBody] RevokeLicenseRequest request)
    {
        try
        {
            LicenseManager.RevokeLicense(request.Key);
            return Ok(new { Message = "License revoked successfully" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    [HttpGet("keys")]
    public async Task<IActionResult> GetValidKeys()
    {
        try
        {
            var keys = await LicenseManager.GetValidKeys();
            return Ok(new { Keys = keys, Count = keys.Count });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }
}

public class AddKeyRequest
{
    public string Key { get; set; } = "";
}

public class RevokeLicenseRequest
{
    public string Key { get; set; } = "";
}
