using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace DentalClinic.Services;

/// <summary>How many requests one visitor (one IP address) may make. All values can be changed in configuration.</summary>
public class RateLimitSettings
{
    public const string Section = "RateLimit";

    /// <summary>Booking an appointment and sending a contact message: both can be used to spam the clinic.</summary>
    [Range(1, 100_000)] public int SubmitPer10Minutes { get; set; } = 8;

    /// <summary>"Find my appointment" and cancelling: stops someone trying many reference codes.</summary>
    [Range(1, 100_000)] public int LookupPer10Minutes { get; set; } = 20;

    /// <summary>The admin login: stops password guessing.</summary>
    [Range(1, 100_000)] public int LoginPerMinute { get; set; } = 8;

    /// <summary>The JSON the booking form loads (free days and times).</summary>
    [Range(1, 100_000)] public int ReadPerMinute { get; set; } = 120;
}

public static class RateLimitPolicies
{
    public const string Submit = "submit";
    public const string Lookup = "lookup";
    public const string Login = "login";
    public const string Read = "read";

    public static IServiceCollection AddClinicRateLimiting(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<RateLimitSettings>()
            .Bind(config.GetSection(RateLimitSettings.Section)).ValidateDataAnnotations().ValidateOnStart();

        services.AddRateLimiter(_ => { });
        // Configured through the options system (not read eagerly) so the limits can be changed in tests and hosting.
        services.AddOptions<RateLimiterOptions>().Configure<IOptions<RateLimitSettings>>((limiter, settings) =>
        {
            var s = settings.Value;
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            Add(limiter, Submit, s.SubmitPer10Minutes, TimeSpan.FromMinutes(10));
            Add(limiter, Lookup, s.LookupPer10Minutes, TimeSpan.FromMinutes(10));
            Add(limiter, Login, s.LoginPerMinute, TimeSpan.FromMinutes(1));
            Add(limiter, Read, s.ReadPerMinute, TimeSpan.FromMinutes(1));

            limiter.OnRejected = async (context, ct) =>
            {
                var response = context.HttpContext.Response;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
                response.StatusCode = StatusCodes.Status429TooManyRequests;
                response.ContentType = "text/plain; charset=utf-8";
                await response.WriteAsync("Previše pokušaja u kratkom vremenu. Sačekajte nekoliko minuta i pokušajte ponovo.", ct);
            };
        });
        return services;
    }

    private static void Add(RateLimiterOptions limiter, string name, int permits, TimeSpan window) =>
        limiter.AddPolicy(name, context => RateLimitPartition.GetFixedWindowLimiter(
            (context.Connection.RemoteIpAddress ?? IPAddress.None).ToString(),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permits, Window = window, QueueLimit = 0 }));
}
