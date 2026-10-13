using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;

namespace PMGM.Api.Infrastructure;

/// <summary>
/// Solo acepta X-Forwarded-For a través de IPs exactas de proxies validados.
/// Sin configuración explícita se conserva la IP del socket, no el header del cliente.
/// </summary>
public static class TrustedProxyForwarding
{
    public static void Configure(ForwardedHeadersOptions options, IConfiguration configuration)
    {
        // Eliminar el loopback implícito: también sería un origen válido para suplantación
        // si se ejecuta otro proceso local. Nunca confiar en una red Docker completa.
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();
        options.ForwardedHeaders = ForwardedHeaders.None;
        options.ForwardLimit = 1;

        var configured = configuration["ForwardedHeaders:KnownProxies"];
        if (string.IsNullOrWhiteSpace(configured))
            return;

        var addresses = configured.Split(',', StringSplitOptions.TrimEntries);
        if (addresses.Length is < 1 or > 2 || addresses.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException(
                "ForwardedHeaders:KnownProxies debe enumerar 1 o 2 direcciones IP exactas, separadas por coma.");

        foreach (var token in addresses)
        {
            if (!IPAddress.TryParse(token, out var address))
                throw new InvalidOperationException(
                    "ForwardedHeaders:KnownProxies requiere IPs exactas, no nombres ni rangos.");

            // Kestrel puede presentar IPv4 del socket como ::ffff:x.y.z.w.
            // Confiar en ambas representaciones de la MISMA IP exacta, nunca en su CIDR.
            var normalized = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
            if (IPAddress.Any.Equals(normalized)
                || IPAddress.IPv6Any.Equals(normalized)
                || IPAddress.None.Equals(normalized)
                || IPAddress.IPv6None.Equals(normalized)
                || IPAddress.IsLoopback(normalized)
                || (normalized.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                    && normalized.GetAddressBytes()[0] >= 224)
                || options.KnownProxies.Contains(normalized))
            {
                throw new InvalidOperationException(
                    "ForwardedHeaders:KnownProxies solo admite IPs de proxy exactas, únicas y no loopback.");
            }

            options.KnownProxies.Add(normalized);
            if (normalized.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                options.KnownProxies.Add(normalized.MapToIPv6());
        }

        // Uno para nginx→API, dos para Caddy→nginx→API.
        // El límite impide consumir un prefijo XFF falso añadido por un atacante.
        options.ForwardLimit = addresses.Length;
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
    }
}
