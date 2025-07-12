

using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
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
    private static readonly List<LicenseData> _licenses = new()
    {
        new LicenseData
        {
            Key = "ABCD-EFGH-IJKL-MNOP",
            HardwareId = "",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds()
        },
        new LicenseData
        {
            Key = "QRST-UVWX-YZAB-CDEF",
            HardwareId = "",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds()
        }
    };

    public static bool ValidateLicense(string key, string hardwareId)
    {
        var license = _licenses.FirstOrDefault(l => l.Key == key);
        if (license == null || !license.IsActive)
            return false;

        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > license.ExpiresAt)
            return false;

        if (string.IsNullOrEmpty(license.HardwareId))
        {
            license.HardwareId = hardwareId;
            return true;
        }

        return license.HardwareId == hardwareId;
    }

    public static void AddLicense(string key, int daysValid = 30)
    {
        _licenses.Add(new LicenseData
        {
            Key = key,
            HardwareId = "",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(daysValid).ToUnixTimeSeconds()
        });
    }

    public static void RevokeLicense(string key)
    {
        var license = _licenses.FirstOrDefault(l => l.Key == key);
        if (license != null)
            license.IsActive = false;
    }
}

[ApiController]
[Route("")]
public class LicenseController : ControllerBase
{
    [HttpPost("validate")]
    public IActionResult ValidateLicense([FromBody] LicenseRequest request)
    {
        try
        {
            var isValid = LicenseManager.ValidateLicense(request.Key, request.HardwareId);
            return Ok(new LicenseResponse
            {
                IsValid = isValid,
                Message = isValid ? "License valid" : "Invalid license or expired"
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

    [HttpPost("add")]
    public IActionResult AddLicense([FromBody] AddLicenseRequest request)
    {
        try
        {
            LicenseManager.AddLicense(request.Key, request.DaysValid);
            return Ok(new { Message = "License added successfully" });
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
}

public class AddLicenseRequest
{
    public string Key { get; set; } = "";
    public int DaysValid { get; set; } = 30;
}

public class RevokeLicenseRequest
{
    public string Key { get; set; } = "";
}
