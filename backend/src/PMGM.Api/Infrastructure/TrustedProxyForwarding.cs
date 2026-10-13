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
            if (!IPAddress.TryParse(token, out var address)
                || IPAddress.Any.Equals(address)
                || IPAddress.IPv6Any.Equals(address)
                || IPAddress.None.Equals(address)
                || IPAddress.IPv6None.Equals(address)
                || IPAddress.IsLoopback(address)
                || (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                    && address.GetAddressBytes()[0] >= 224)
                || options.KnownProxies.Contains(address))
            {
                throw new InvalidOperationException(
                    "ForwardedHeaders:KnownProxies solo admite IPs de proxy exactas, únicas y no loopback.");
            }

            options.KnownProxies.Add(address);
        }

        // Uno para nginx→API, dos para Caddy→nginx→API.
        // El límite impide consumir un prefijo XFF falso añadido por un atacante.
        options.ForwardLimit = addresses.Length;
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
    }
}
