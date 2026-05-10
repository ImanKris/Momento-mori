using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using RoboStore.Data;

namespace RoboStore.Controllers;

[ApiController]
[Route("[controller]")]
public class HealthController : Controller
{
    private readonly IConfiguration _configuration;

    public HealthController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Lightweight DB availability check - uses a minimal query
    /// </summary>
    [HttpGet("db")]
    public IActionResult DbCheck()
    {
        try
        {
            var connectionString = GetConnectionString();
            using var conn = new SqlConnection(connectionString);
            conn.Open();

            // Minimal check - just verify connection opens
            using var cmd = new SqlCommand("SELECT 1", conn);
            cmd.ExecuteScalar();

            return Json(new
            {
                status = "ok",
                database = "connected",
                timestamp = DateTime.UtcNow
            });
        }
        catch
        {
            return Json(new
            {
                status = "error",
                database = "disconnected",
                message = "Database unavailable",
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Full health check with more details
    /// </summary>
    [HttpGet("full")]
    public IActionResult FullCheck()
    {
        var result = new
        {
            status = "ok",
            timestamp = DateTime.UtcNow,
            database = "unknown",
            version = "1.0"
        };

        try
        {
            var connectionString = GetConnectionString();
            using var conn = new SqlConnection(connectionString);
            conn.Open();
            using var cmd = new SqlCommand("SELECT @@VERSION", conn);
            var version = cmd.ExecuteScalar()?.ToString() ?? "unknown";

            return Json(new
            {
                status = "ok",
                database = "connected",
                dbVersion = version.Split('\n')[0],
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception)
        {
            return Json(new
            {
                status = "degraded",
                database = "error",
                message = "Database check failed",
                timestamp = DateTime.UtcNow
            });
        }
    }

    private string GetConnectionString()
    {
        // Try to get from configuration first, fallback to hardcoded for safety
        var connString = _configuration.GetConnectionString("RoboStore")
            ?? @"Server=RoboStore.mssql.somee.com;Database=RoboStore;User Id=MomentoMori_SQLLogin_1;Password=8rhd2k6i2g;TrustServerCertificate=True";
        return connString;
    }
}
