using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Data;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Models;
using Kotova.CommonClasses;


namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API
{
    public class LegacyAuthenticationService
    {
        //private readonly HttpClient _httpClient;

        private readonly ApplicationDbContext _context;

        /*
        public LegacyAuthenticationService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }
        */
        public LegacyAuthenticationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<bool> PerformLogin(string username, string password)
        {
            // Check username and password
            bool isAuthenticated = await AuthenticateUserAsync(username, password);
            if (isAuthenticated)
            {
                /*
                // Generate 2FA code
                string twoFactorCode = GenerateTwoFactorCode();
                // Save the code in the database or cache with a timestamp
                SaveTwoFactorCode(username, twoFactorCode);
                // Send the code via email
                await SendTwoFactorCodeEmail(username, twoFactorCode); //THIS ALL CODE IS FOR 2-FACTOR AUTHENTICATION
                */
                return true;
            }
            return false;
        }

        
        /*

        public async Task SendTwoFactorCodeEmail(string username, string code)
        {
            var user = GetUserByUsername(username); // Retrieve user info from the database
            var emailService = new EmailService(); // This would be ideally injected via DI
            await emailService.SendEmailAsync(user.Email, "Your 2FA Code", $"Your code is: {code}");
        }
        public bool VerifyTwoFactorCode(string username, string inputCode)
        {
            var storedCode = GetStoredTwoFactorCode(username); // Get the stored 2FA code
            return storedCode != null && storedCode.Code == inputCode && storedCode.Expiry > DateTime.UtcNow;
        }

        */

        public async Task<bool> AuthenticateUserAsync(string username, string password)
        {
            // Fetch the user from the database
            var user = await _context.Users.FirstOrDefaultAsync(u => u.username == username);

            // Check if user exists and password matches
            if (user != null && VerifyPassword(password, user.password_hash))
            {
                return true;
            }
            return false;
        }


        private bool VerifyPassword(string providedPassword, string storedHash)
        {
            // Implement password verification logic here
            // This could be a simple comparison or a more complex hash verification
            return providedPassword == storedHash; // Simplified for illustration
        }
        /* РАЗБЛОКИРУЙ ЭТО И ПРОДОЛЖАЙ!
        public async Task<ApplicationUser> GetByUserNameAndPassword(string username, string password)
        {
            if (await (PerformLogin(username, password)))
            {
            }
        }

        /*

        public async Task<bool> AuthenticateUserAsyncLegacy(string username, string password)
        {
            var legacyCredentials = ConvertToLegacyFormat(username, password);
            var response = await SendAuthenticationRequestToLegacySystem(legacyCredentials);
            return ParseLegacyResponse(response);
        }

        private string ConvertToLegacyFormat(string username, string password)
        {
            // Convert credentials into a legacy format
            // Here we just concatenate them for simplicity
            return $"{username}:{password}";
        }

        private async Task<string> SendAuthenticationRequestToLegacySystem(string credentials)
        {
                var content = new StringContent(credentials, Encoding.UTF8, "text/plain");
                var response = await _httpClient.PostAsync("http://legacy-auth-system.example.com/auth", content);
                return await response.Content.ReadAsStringAsync();
        }

        private bool ParseLegacyResponse(string response)
        {
            // Assume the legacy system returns "true" or "false"
            return response.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
        }
        */
    }
}
