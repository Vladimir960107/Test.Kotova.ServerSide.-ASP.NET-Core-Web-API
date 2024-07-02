using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;


namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Services
{


    public class JwtTokenValidator
    {
        private readonly IConfiguration _configuration;

        public JwtTokenValidator(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public ClaimsPrincipal ValidateToken(string token)
        {
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
            catch
            {
                // Token validation failed
                return null;
            }
        }
    }
}
