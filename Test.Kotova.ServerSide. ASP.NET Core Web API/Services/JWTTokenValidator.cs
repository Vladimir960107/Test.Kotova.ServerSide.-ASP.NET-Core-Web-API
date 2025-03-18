using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Services
{
    /// <summary>
    /// Service for JWT token validation and management
    /// </summary>
    public class JWTTokenValidator
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<JWTTokenValidator> _logger;
        private readonly ConcurrentDictionary<string, DateTime> _blacklistedTokens;
        private Timer _cleanupTimer;

        public JWTTokenValidator(IConfiguration configuration, ILogger<JWTTokenValidator> logger = null)
        {
            _configuration = configuration;
            _logger = logger;
            _blacklistedTokens = new ConcurrentDictionary<string, DateTime>();
            
            // Start a timer to clean up expired tokens from the blacklist
            _cleanupTimer = new Timer(CleanupBlacklist, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
        }

        /// <summary>
        /// Validates a JWT token and returns claims principal
        /// </summary>
        /// <param name="token">The JWT token to validate</param>
        /// <returns>ClaimsPrincipal if token is valid, null otherwise</returns>
        public ClaimsPrincipal ValidateToken(string token)
        {
            // Check if token is blacklisted
            if (_blacklistedTokens.ContainsKey(token))
            {
                _logger?.LogWarning("Attempt to use blacklisted token");
                return null;
            }

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_configuration["JwtConfig:Secret"]);
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _configuration["JwtConfig:Issuer"],
                ValidAudience = _configuration["JwtConfig:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(key)
            };

            try
            {
                var principal = tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);
                return principal;
            }
            catch (Exception ex)
            {
                // Token validation failed
                _logger?.LogWarning(ex, "Token validation failed");
                return null;
            }
        }

        /// <summary>
        /// Gets the user ID from a JWT token
        /// </summary>
        /// <param name="token">The JWT token</param>
        /// <returns>The user ID or null if not found</returns>
        public string GetUserIdFromToken(string token)
        {
            var principal = ValidateToken(token);
            return principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }

        /// <summary>
        /// Gets the username from a JWT token
        /// </summary>
        /// <param name="token">The JWT token</param>
        /// <returns>The username or null if not found</returns>
        public string GetUsernameFromToken(string token)
        {
            var principal = ValidateToken(token);
            return principal?.FindFirst(ClaimTypes.Name)?.Value;
        }

        /// <summary>
        /// Gets the role from a JWT token
        /// </summary>
        /// <param name="token">The JWT token</param>
        /// <returns>The role or null if not found</returns>
        public string GetRoleFromToken(string token)
        {
            var principal = ValidateToken(token);
            return principal?.FindFirst(ClaimTypes.Role)?.Value;
        }

        /// <summary>
        /// Gets the department ID from a JWT token
        /// </summary>
        /// <param name="token">The JWT token</param>
        /// <returns>The department ID or null if not found</returns>
        public string GetDepartmentIdFromToken(string token)
        {
            var principal = ValidateToken(token);
            return principal?.FindFirst("DepartmentId")?.Value;
        }

        /// <summary>
        /// Gets a specific claim value from the token
        /// </summary>
        /// <param name="token">The JWT token</param>
        /// <param name="claimType">The type of claim to retrieve</param>
        /// <returns>The claim value or null if not found</returns>
        public string GetClaimFromToken(string token, string claimType)
        {
            var principal = ValidateToken(token);
            return principal?.FindFirst(claimType)?.Value;
        }

        /// <summary>
        /// Validates a token without checking its lifetime
        /// Useful for analyzing expired tokens
        /// </summary>
        /// <param name="token">The JWT token</param>
        /// <returns>ClaimsPrincipal if token signature is valid, null otherwise</returns>
        public ClaimsPrincipal ValidateTokenIgnoringExpiration(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_configuration["JwtConfig:Secret"]);
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = false, // Do not validate lifetime
                ValidateIssuerSigningKey = true,
                ValidIssuer = _configuration["JwtConfig:Issuer"],
                ValidAudience = _configuration["JwtConfig:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(key)
            };

            try
            {
                var principal = tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);
                return principal;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Token validation (ignoring expiration) failed");
                return null;
            }
        }

        /// <summary>
        /// Blacklists a JWT token to prevent it from being used
        /// </summary>
        /// <param name="token">The JWT token to blacklist</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool BlacklistToken(string token)
        {
            try
            {
                // Validate token structure (but ignore expiration)
                var principal = ValidateTokenIgnoringExpiration(token);
                if (principal == null)
                {
                    _logger?.LogWarning("Cannot blacklist invalid token");
                    return false;
                }

                // Get the token expiry time from the claims
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);
                var expires = jwtToken.ValidTo;

                // Add to blacklist
                _blacklistedTokens.TryAdd(token, expires);
                _logger?.LogInformation("Token blacklisted successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error blacklisting token");
                return false;
            }
        }

        /// <summary>
        /// Checks if a token is blacklisted
        /// </summary>
        /// <param name="token">The JWT token</param>
        /// <returns>True if blacklisted, false otherwise</returns>
        public bool IsTokenBlacklisted(string token)
        {
            return _blacklistedTokens.ContainsKey(token);
        }

        /// <summary>
        /// Gets the expiration time of a token
        /// </summary>
        /// <param name="token">The JWT token</param>
        /// <returns>The expiration time or null if token is invalid</returns>
        public DateTime? GetTokenExpirationTime(string token)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);
                return jwtToken.ValidTo;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error reading token expiration time");
                return null;
            }
        }

        /// <summary>
        /// Cleans up expired tokens from the blacklist
        /// </summary>
        private void CleanupBlacklist(object state)
        {
            try
            {
                var now = DateTime.UtcNow;
                var expiredTokens = new List<string>();

                // Find expired tokens
                foreach (var item in _blacklistedTokens)
                {
                    if (item.Value < now)
                    {
                        expiredTokens.Add(item.Key);
                    }
                }

                // Remove expired tokens from blacklist
                foreach (var token in expiredTokens)
                {
                    _blacklistedTokens.TryRemove(token, out _);
                }

                _logger?.LogInformation($"Cleaned up {expiredTokens.Count} expired tokens from blacklist");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error cleaning up token blacklist");
            }
        }
    }
}
