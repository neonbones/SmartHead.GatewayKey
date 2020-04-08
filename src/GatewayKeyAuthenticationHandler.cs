using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SmartHead.GatewayKey
{
    public class GatewayKeyAuthenticationHandler : AuthenticationHandler<GatewayKeyAuthenticationOptions>
    {
        private readonly DbContext _context;

        public GatewayKeyAuthenticationHandler(
            IOptionsMonitor<GatewayKeyAuthenticationOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock,
            DbContext dbContext) : base(options, logger, encoder, clock)
        {
            _context = dbContext;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(Options.GatewayKeyHeaderName, out var gatewayKeyHeaderValues))
                return AuthenticateResult.NoResult();

            var header = gatewayKeyHeaderValues.FirstOrDefault();

            if (gatewayKeyHeaderValues.Count == 0 || string.IsNullOrWhiteSpace(header))
                return AuthenticateResult.NoResult();

            var key = await _context
                .Set<GatewayKeyBase>()
                .FirstOrDefaultAsync(x => x.Key == header);

            if (key == null || !key.IsActive)
                return AuthenticateResult.Fail("Invalid Gateway Key.");

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.GivenName, key.Name)
            };

            switch (key.GatewayType)
            {
                case GatewayKeyBase.Type.Microservice:
                    claims.Add(new Claim(ClaimTypes.Spn, nameof(GatewayKeyBase.Type.Microservice)));
                    break;
                case GatewayKeyBase.Type.Device:
                    claims.Add(new Claim(ClaimTypes.Spn, nameof(GatewayKeyBase.Type.Device)));
                    break;
                case GatewayKeyBase.Type.ThirdParty:
                    claims.Add(new Claim(ClaimTypes.Spn, nameof(GatewayKeyBase.Type.ThirdParty)));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var ticket = new AuthenticationTicket(
                new ClaimsPrincipal(
                    new[] {new ClaimsIdentity(claims, Options.AuthenticationType)}), Options.Scheme);

            return AuthenticateResult.Success(ticket);
        }
    }
}