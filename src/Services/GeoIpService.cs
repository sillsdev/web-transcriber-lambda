using System.Net;
using System.Reflection;
using System.Text.Json;

namespace SIL.Transcriber.Services
{
    public class GeoIpService(
        ILoggerFactory loggerFactory,
        IHttpContextAccessor httpContextAccessor)
    {
        private readonly HttpClient _client = new();
        private readonly HttpContext? _httpContext = httpContextAccessor.HttpContext;
        private readonly ILogger Logger = loggerFactory.CreateLogger<GeoIpService>();

        public async Task<string> GetCountryAsync(CancellationToken cancellationToken = default)
        {
            string? clientIp = GetClientIp();
            Logger.LogInformation("Geo IP lookup starting. ClientIp={ClientIp}", clientIp ?? "<none>");

            if (string.IsNullOrWhiteSpace(clientIp))
            {
                Logger.LogInformation("Geo IP lookup returning Unknown because no public client IP was found.");
                return "Unknown";
            }

            string? country = await ResolveCountryAsync(clientIp, cancellationToken);
            Logger.LogInformation("Geo IP lookup finished. ClientIp={ClientIp}, Country={Country}", clientIp, country ?? "<null>");
            return string.IsNullOrWhiteSpace(country) ? "Unknown" : country;
        }

        private string? GetClientIp()
        {
            string? forwardedFor = _httpContext?.Request.Headers["X-Forwarded-For"].FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(forwardedFor))
            {
                foreach (string part in forwardedFor.Split(','))
                {
                    string ip = part.Trim();
                    if (IsPublicIp(ip))
                    {
                        return ip;
                    }
                }
            }

            string? apiGatewaySourceIp = GetApiGatewaySourceIp();
            if (IsPublicIp(apiGatewaySourceIp))
            {
                return apiGatewaySourceIp;
            }

            string? remoteIp = _httpContext?.Connection.RemoteIpAddress?.ToString();

            return IsPublicIp(remoteIp) ? remoteIp : null;
        }

        private string? GetApiGatewaySourceIp()
        {
            if (_httpContext == null)
            {
                return null;
            }

            foreach (object feature in _httpContext.Features)
            {
                string? sourceIp = TryGetSourceIp(feature);
                if (!string.IsNullOrWhiteSpace(sourceIp))
                {
                    return sourceIp;
                }
            }

            return null;
        }

        private static string? TryGetSourceIp(object feature)
        {
            object? requestContext = GetPropertyValue(feature, "RequestContext");
            if (requestContext == null)
            {
                return null;
            }

            object? identity = GetPropertyValue(requestContext, "Identity");
            if (identity == null)
            {
                return null;
            }

            object? sourceIp = GetPropertyValue(identity, "SourceIp");
            return sourceIp as string;
        }

        private static object? GetPropertyValue(object instance, string propertyName)
        {
            PropertyInfo? property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            return property?.GetValue(instance);
        }

        private static bool IsPublicIp(string? ip)
        {
            if (!IPAddress.TryParse(ip, out IPAddress? address))
            {
                return false;
            }

            if (IPAddress.IsLoopback(address))
            {
                return false;
            }

            byte[] bytes = address.GetAddressBytes();
            return address.AddressFamily switch
            {
                System.Net.Sockets.AddressFamily.InterNetwork =>
                    !(bytes[0] == 10 ||
                      (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
                      (bytes[0] == 192 && bytes[1] == 168) ||
                      (bytes[0] == 169 && bytes[1] == 254)),
                System.Net.Sockets.AddressFamily.InterNetworkV6 => !address.IsIPv6LinkLocal && !address.IsIPv6SiteLocal,
                _ => false
            };
        }

        private async Task<string?> ResolveCountryAsync(string ip, CancellationToken cancellationToken)
        {
            try
            {
                using HttpResponseMessage response = await _client.GetAsync($"https://ipwho.is/{ip}", cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    Logger.LogWarning("Geo IP lookup failed for {Ip} with status {StatusCode}", ip, response.StatusCode);
                    return null;
                }

                string payload = await response.Content.ReadAsStringAsync(cancellationToken);
                using JsonDocument document = JsonDocument.Parse(payload);

                bool success = document.RootElement.TryGetProperty("success", out JsonElement successElement) && successElement.GetBoolean();
                if (!success)
                {
                    string? message = document.RootElement.TryGetProperty("message", out JsonElement messageElement)
                        ? messageElement.GetString()
                        : null;
                    Logger.LogWarning("Geo IP lookup returned failure for {Ip}: {Message}", ip, message ?? "<no message>");
                    return null;
                }

                string? countryCode = document.RootElement.TryGetProperty("country_code", out JsonElement countryCodeElement)
                    ? countryCodeElement.GetString()
                    : null;
                string? country = document.RootElement.TryGetProperty("country", out JsonElement countryElement)
                    ? countryElement.GetString()
                    : null;

                return !string.IsNullOrWhiteSpace(countryCode) && countryCode.Length == 2 ? countryCode : country;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Geo IP lookup failed for {Ip}", ip);
                return null;
            }
        }
    }
}
