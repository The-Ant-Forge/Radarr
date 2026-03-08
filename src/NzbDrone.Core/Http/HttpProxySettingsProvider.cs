using System;
using System.Linq;
using System.Net;
using NzbDrone.Common.Http;
using NzbDrone.Common.Http.Proxy;
using NzbDrone.Core.Configuration;

namespace NzbDrone.Core.Http
{
    public class HttpProxySettingsProvider : IHttpProxySettingsProvider
    {
        private readonly IConfigService _configService;

        public HttpProxySettingsProvider(IConfigService configService)
        {
            _configService = configService;
        }

        public HttpProxySettings GetProxySettings(HttpUri uri)
        {
            var proxySettings = GetProxySettings();
            if (proxySettings == null)
            {
                return null;
            }

            if (ShouldProxyBeBypassed(proxySettings, uri))
            {
                return null;
            }

            return proxySettings;
        }

        public HttpProxySettings GetProxySettings()
        {
            if (!_configService.ProxyEnabled)
            {
                return null;
            }

            return new HttpProxySettings(_configService.ProxyType,
                                _configService.ProxyHostname,
                                _configService.ProxyPort,
                                _configService.ProxyBypassFilter,
                                _configService.ProxyBypassLocalAddresses,
                                _configService.ProxyUsername,
                                _configService.ProxyPassword);
        }

        public bool ShouldProxyBeBypassed(HttpProxySettings proxySettings, HttpUri url)
        {
            // We are utilizing the WebProxy implementation here to save us having to re-implement it. This way we use Microsofts implementation
            var proxy = new WebProxy(proxySettings.Host + ":" + proxySettings.Port, proxySettings.BypassLocalAddress, proxySettings.BypassListAsArray);

            return proxy.IsBypassed((Uri)url) || IsBypassedByIpAddressRange(proxySettings.BypassListAsArray, url.Host);
        }

        private static bool IsBypassedByIpAddressRange(string[] bypassList, string host)
        {
            if (!IPAddress.TryParse(host, out var hostAddress))
            {
                return false;
            }

            return bypassList.Any(bypass => IsInCidrRange(hostAddress, bypass));
        }

        private static bool IsInCidrRange(IPAddress address, string cidr)
        {
            var slashIndex = cidr.IndexOf('/');
            if (slashIndex < 0)
            {
                return IPAddress.TryParse(cidr, out var exact) && exact.Equals(address);
            }

            if (!IPAddress.TryParse(cidr.Substring(0, slashIndex), out var network))
            {
                return false;
            }

            if (!int.TryParse(cidr.Substring(slashIndex + 1), out var prefixLength))
            {
                return false;
            }

            var networkBytes = network.GetAddressBytes();
            var maxPrefix = networkBytes.Length * 8;

            if (prefixLength < 0 || prefixLength > maxPrefix)
            {
                return false;
            }
            var addressBytes = address.GetAddressBytes();

            if (networkBytes.Length != addressBytes.Length)
            {
                return false;
            }

            var fullBytes = prefixLength / 8;
            var remainingBits = prefixLength % 8;

            for (var i = 0; i < fullBytes; i++)
            {
                if (networkBytes[i] != addressBytes[i])
                {
                    return false;
                }
            }

            if (remainingBits > 0 && fullBytes < networkBytes.Length)
            {
                var mask = (byte)(0xFF << (8 - remainingBits));
                if ((networkBytes[fullBytes] & mask) != (addressBytes[fullBytes] & mask))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
