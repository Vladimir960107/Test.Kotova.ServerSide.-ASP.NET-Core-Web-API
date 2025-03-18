using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Data;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Models;
using Kotova.CommonClasses;
using DocumentFormat.OpenXml.Drawing.Diagrams;


namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API
{
    public class LegacyAuthenticationService
    {
        //private readonly HttpClient _httpClient;

        private readonly ApplicationDbContextUsers _context;

        /*
        public LegacyAuthenticationService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }
        */
        public LegacyAuthenticationService(ApplicationDbContextUsers context)
        {
            _context = context;
        }

        public (bool?, User?) PerformLogin(User userTemp, string plainPassword)
        {

            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(plainPassword, userTemp.password_hash);

            if (!isPasswordValid)
            {
                return (false, null);
            }

            if (userTemp.current_email is not null)
            {
                /*
                // Generate 2FA code
                string twoFactorCode = GenerateTwoFactorCode();
                // Save the code in the database or cache with a timestamp
                SaveTwoFactorCode(username, twoFactorCode);
                // Send the code via email
                await SendTwoFactorCodeEmail(username, twoFactorCode); //THIS ALL CODE IS FOR 2-FACTOR AUTHENTICATION
                IF AUTHENTICATED - 
                return (true, userTemp);
                IF NOT AUTHENTICATED -
                return (false, userTemp);
                */
                // IN CASE HE IS NOT AUTHENTICATED - CODE AT THE TOP THAT IS COMMENTED SHOULD RETURN FALSE AND STUFF!
                return (true, userTemp);
            }
            if (userTemp.current_email is null) //ДЛЯ ВОДИТЕЛЕЙ И НЕАВТОРИЗИРОВАННЫХ ПОЛЬЗОВАТЕЛЕЙ, МОЖЕТ ПО НОМЕРУ ТЕЛЕФОНА?
            {
                //return something
                //CHECK BY NUMBER OR JUST FORGET ABOUT IT ¯\_(ツ)_/¯
                return (true,userTemp); //gonna return (true, User)
            }
            if (userTemp.current_personnel_number is null) // ДЛЯ  ПОЛЬЗОВАТЕЛЕЙ БЕЗ ПЕРСОНАЛЬНОГО НОМЕРА!
            {
                return (null, userTemp); // gonna return (null,User)
            }
            return (false, userTemp); // gonna return (false, User)
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

        public async Task<(bool?, User?)> SimpleAuthenticationUserAsync(string username, string password)
        {
            // Fetch the user from the database
            var user = await _context.Users.FirstOrDefaultAsync(u => u.username == username);

            if (user == null || !VerifyPassword(password, user.password_hash))
            {
                return (false, null);
            }
            // Check if user exists and password matches
            if (user.current_personnel_number is null)
            {
                return (null, user);

            }
            return (true, user);

        }


        private bool VerifyPassword(string providedPassword, string storedHash)
        {
            // Implement password verification logic here
            // This could be a simple comparison or a more complex hash verification
            return providedPassword == storedHash; // Simplified for illustration
        }
        // Вроде как это уже не нужно. Можешь убрать?
        /*
        public async Task<ApplicationUser> GetByUserNameAndPassword(string username, string password)
        {
            if (await (PerformLogin(username, password)))
            {
            }
        }
        */
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
