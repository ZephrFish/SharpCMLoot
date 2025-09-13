using System.Text.RegularExpressions;
using SCML.Models;

namespace SCML.Services
{
    public static class CredentialParser
    {
        public static Credentials Parse(string target)
        {
            var credentials = new Credentials();

            // Parse format: [[domain\]username[:password]@]<address>
            var pattern = @"^(?:(?:(?<domain>[^\\@:]+)\\)?(?<username>[^@:]+)(?::(?<password>[^@]+))?@)?(?<address>.+)$";
            var match = Regex.Match(target, pattern);

            if (match.Success)
            {
                credentials.Domain = match.Groups["domain"].Success ? match.Groups["domain"].Value : null;
                credentials.Username = match.Groups["username"].Success ? match.Groups["username"].Value : null;
                credentials.Password = match.Groups["password"].Success ? match.Groups["password"].Value : null;
                credentials.Address = match.Groups["address"].Value;
            }
            else
            {
                credentials.Address = target;
            }

            return credentials;
        }
    }
}