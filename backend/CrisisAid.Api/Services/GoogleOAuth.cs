using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CrisisAid.Api.Data;
using CrisisAid.Api.Domain;
using CrisisAid.Api.Services;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace CrisisAid.Api.Services;

/// <summary>
/// Google OAuth 2.0 Authorization Code + PKCE (localhost coursework integration).
/// Google's ID token is checked against Google's public signing keys; no user-controlled
/// Google profile values are accepted without issuer, audience, expiry and nonce validation.
/// An expiring one-time ticket is exchanged via POST to avoid putting the API JWT in a URL.
/// </summary>
public static class GoogleOAuth
{
    private sealed record LoginTicket(string Jwt, DateTime Expires, object User, DateTime TicketExpires);
    private static readonly ConcurrentDictionary<string, LoginTicket> Tickets = new();
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(5);
    private const string StateCookie = "crisisaid_google_state";
    private const string VerifierCookie = "crisisaid_google_verifier";
    private const string NonceCookie = "crisisaid_google_nonce";

    public sealed record ExchangeRequest(string Ticket);

    public static void Map(WebApplication app)
    {
        app.MapGet("/api/auth/google/config", (IConfiguration cfg) => Results.Ok(new {
            enabled = Enabled(cfg),
            redirectUri = cfg["Google:RedirectUri"] ?? "http://localhost:8080/api/auth/google/callback"
        })).AllowAnonymous();

        app.MapGet("/api/auth/google/start", (HttpContext ctx, IConfiguration cfg) =>
        {
            if (!Enabled(cfg)) return Results.BadRequest(new { error = "Google sign-in is not configured. Add GOOGLE_CLIENT_ID and GOOGLE_CLIENT_SECRET to .env." });
            var state = RandomText();
            var nonce = RandomText();
            var verifier = RandomText();
            var cookieOptions = new CookieOptions {
                HttpOnly = true, Secure = ctx.Request.IsHttps, SameSite = SameSiteMode.Lax,
                MaxAge = StateLifetime, Path = "/api/auth/google"
            };
            ctx.Response.Cookies.Append(StateCookie, state, cookieOptions);
            ctx.Response.Cookies.Append(VerifierCookie, verifier, cookieOptions);
            ctx.Response.Cookies.Append(NonceCookie, nonce, cookieOptions);
            var challenge = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
            var uri = QueryHelpers.AddQueryString("https://accounts.google.com/o/oauth2/v2/auth", new Dictionary<string, string?> {
                ["client_id"] = cfg["Google:ClientId"],
                ["redirect_uri"] = RedirectUri(cfg),
                ["response_type"] = "code",
                ["scope"] = "openid email profile",
                ["state"] = state,
                ["nonce"] = nonce,
                ["code_challenge"] = challenge,
                ["code_challenge_method"] = "S256",
                ["prompt"] = "select_account"
            });
            return Results.Redirect(uri);
        }).AllowAnonymous().RequireRateLimiting("auth");

        app.MapGet("/api/auth/google/callback", async (HttpContext ctx, IConfiguration cfg,
            AidDbContext db, IHttpClientFactory httpFactory) =>
        {
            var frontend = FrontendUrl(cfg);
            IResult Failure(string error) => Results.Redirect(frontend + "/?google_error=" + Uri.EscapeDataString(error));
            if (!Enabled(cfg)) return Failure("Google sign-in is not configured");
            var state = ctx.Request.Query["state"].ToString();
            var oldState = ctx.Request.Cookies[StateCookie];
            var verifier = ctx.Request.Cookies[VerifierCookie];
            var nonce = ctx.Request.Cookies[NonceCookie];
            ctx.Response.Cookies.Delete(StateCookie, new CookieOptions { Path = "/api/auth/google" });
            ctx.Response.Cookies.Delete(VerifierCookie, new CookieOptions { Path = "/api/auth/google" });
            ctx.Response.Cookies.Delete(NonceCookie, new CookieOptions { Path = "/api/auth/google" });
            if (ctx.Request.Query.ContainsKey("error")) return Failure("Google sign-in was cancelled or denied");
            if (string.IsNullOrEmpty(oldState) || string.IsNullOrEmpty(state) ||
                !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(state), Encoding.UTF8.GetBytes(oldState)) ||
                string.IsNullOrEmpty(verifier) || string.IsNullOrEmpty(nonce))
                return Failure("Google sign-in state expired or failed verification. Please retry");
            var code = ctx.Request.Query["code"].ToString();
            if (string.IsNullOrWhiteSpace(code)) return Failure("Google did not return an authorisation code");
            try
            {
                var client = httpFactory.CreateClient("google-oauth");
                using var tokenResponse = await client.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(new Dictionary<string, string> {
                    ["code"] = code,
                    ["client_id"] = cfg["Google:ClientId"]!,
                    ["client_secret"] = cfg["Google:ClientSecret"]!,
                    ["redirect_uri"] = RedirectUri(cfg),
                    ["grant_type"] = "authorization_code",
                    ["code_verifier"] = verifier
                }));
                if (!tokenResponse.IsSuccessStatusCode) return Failure("Google token exchange failed. Check OAuth redirect URI and credentials");
                using var tokens = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync());
                var idToken = tokens.RootElement.GetProperty("id_token").GetString();
                if (string.IsNullOrEmpty(idToken)) return Failure("Google did not provide an ID token");
                var jwksJson = await client.GetStringAsync("https://www.googleapis.com/oauth2/v3/certs");
                var jwks = new JsonWebKeySet(jwksJson);
                var parameters = new TokenValidationParameters {
                    ValidateIssuer = true, ValidIssuers = new[] { "https://accounts.google.com", "accounts.google.com" },
                    ValidateAudience = true, ValidAudience = cfg["Google:ClientId"],
                    ValidateLifetime = true, RequireExpirationTime = true, ValidateIssuerSigningKey = true,
                    IssuerSigningKeys = jwks.Keys,
                    ValidAlgorithms = new[] { SecurityAlgorithms.RsaSha256 },
                    ClockSkew = TimeSpan.FromMinutes(2)
                };
                var principal = new JwtSecurityTokenHandler().ValidateToken(idToken, parameters, out _);
                string? Claim(string name) => principal.FindFirst(name)?.Value;
                var tokenNonce = Claim("nonce");
                if (string.IsNullOrEmpty(tokenNonce) || !CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(tokenNonce), Encoding.UTF8.GetBytes(nonce))) return Failure("Invalid Google login nonce");
                var verified = Claim("email_verified");
                var email = (Claim("email") ?? Claim(ClaimTypes.Email) ?? "").Trim().ToLowerInvariant();
                if (!string.Equals(verified, "true", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(email) || !email.Contains('@') || string.IsNullOrWhiteSpace(Claim("sub") ?? Claim(ClaimTypes.NameIdentifier)))
                    return Failure("A verified Google email address is required");
                var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email);
                if (user != null && !user.IsActive) return Failure("Account is disabled");
                if (user is null)
                {
                    var roleDef = await db.RoleDefinitions.FirstOrDefaultAsync(x => x.Name == Roles.Citizen);
                    user = new UserAccount {
                        Email = email, DisplayName = (Claim("name") ?? email.Split('@')[0])[..Math.Min((Claim("name") ?? email.Split('@')[0]).Length,120)],
                        Role = Roles.Citizen, RoleId = roleDef?.Id,
                        // Google-only account has no locally usable password.
                        PasswordHash = "GOOGLE_ONLY"
                    };
                    db.Users.Add(user);
                    TraceLog.Add(db, "User", user.Id, "GoogleRegistered", user.Id);
                }
                TraceLog.Add(db, "User", user.Id, "GoogleSignedIn", user.Id);
                await db.SaveChangesAsync();
                var session = CreateSession(user, cfg);
                var ticket = RandomText();
                Tickets[ticket] = session;
                foreach (var entry in Tickets.Where(x => x.Value.TicketExpires < DateTime.UtcNow).ToArray())
                    Tickets.TryRemove(entry.Key, out _);
                return Results.Redirect(frontend + "/?google_ticket=" + Uri.EscapeDataString(ticket));
            }
            catch (Exception ex)
            {
                app.Logger.LogError(ex, "Google OAuth callback failed");
                return Failure("Google sign-in verification failed. Check Google settings and try again");
            }
        }).AllowAnonymous().RequireRateLimiting("auth");

        app.MapPost("/api/auth/google/exchange", (ExchangeRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.Ticket) || !Tickets.TryRemove(request.Ticket, out var entry) || entry.TicketExpires < DateTime.UtcNow)
                return Results.Unauthorized();
            return Results.Ok(new { token = entry.Jwt, expires = entry.Expires, user = entry.User });
        }).AllowAnonymous().RequireRateLimiting("auth");
    }

    private static LoginTicket CreateSession(UserAccount user, IConfiguration cfg)
    {
        var claims = new List<Claim> {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()), new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Email, user.Email), new(ClaimTypes.Role, user.Role)
        };
        var expires = DateTime.UtcNow.AddHours(6);
        var jwt = new JwtSecurityToken(cfg["Jwt:Issuer"] ?? "CrisisAid", cfg["Jwt:Audience"] ?? "CrisisAid.Web", claims,
            expires: expires, signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!)), SecurityAlgorithms.HmacSha256));
        return new LoginTicket(new JwtSecurityTokenHandler().WriteToken(jwt), expires,
            new { user.Id, user.DisplayName, user.Email, user.Role }, DateTime.UtcNow.AddSeconds(90));
    }
    private static string RandomText() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    private static string RedirectUri(IConfiguration cfg) => cfg["Google:RedirectUri"] ?? "http://localhost:8080/api/auth/google/callback";
    private static string FrontendUrl(IConfiguration cfg) => (cfg["Google:FrontendUrl"] ?? "http://localhost:5173").TrimEnd('/');
    private static bool Enabled(IConfiguration cfg) => !string.IsNullOrWhiteSpace(cfg["Google:ClientId"]) && !string.IsNullOrWhiteSpace(cfg["Google:ClientSecret"]);
}
