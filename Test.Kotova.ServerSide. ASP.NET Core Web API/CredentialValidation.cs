using Kotova.CommonClasses;
using System.Text.RegularExpressions;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API
{
    public class CredentialValidation
    {
        // Regex for validating login - we'll allow letters, numbers, and underscore
        private const string LoginRegex = @"^[a-zA-Z0-9_]+$";

        // Regex for validating password - at least one lowercase letter, one uppercase letter, one number, and is at least 8 characters long
        private const string PasswordRegex = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$";

        // Regex for validating email. A common regex for email validation.
        private const string EmailRegex = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";

        public bool CheckForValidation(UserCredentials temp, string oldusername)
        {
            string newusername = temp.Login;
            string password = temp.Password;
            string email = temp.Email;
            if (!ValidateLogin(newusername) || string.IsNullOrWhiteSpace(newusername))
            {
                Console.WriteLine($"{newusername} is not valid. old user name - {oldusername}");
                return false; // false MEANS NOT GOOD RESPONSE
            }
            else if (!ValidatePassword(password) || string.IsNullOrWhiteSpace(password)) 
            {
                Console.WriteLine($"{password} is not valid. old user name - {oldusername}");
                return false;
            }
            else if (string.IsNullOrWhiteSpace(email) || (!ValidateEmail(email)))
            {
                Console.WriteLine($"{email} is not valid. old user name - {oldusername}");
                return false;
            }
            return true;
        }
        private bool ValidateLogin(string login)
        {
            return Regex.IsMatch(login, LoginRegex);
        }

        private bool ValidatePassword(string password)
        {
            return Regex.IsMatch(password, PasswordRegex);
        }

        private bool ValidatePasswordsMatch(string password, string repeatedPassword)
        {
            return password == repeatedPassword;
        }

        private bool ValidateEmail(string email)
        {
            return string.IsNullOrEmpty(email) || Regex.IsMatch(email, EmailRegex);
        }
    }
}
