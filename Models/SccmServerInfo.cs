using System;

namespace SCML.Models
{
    public class SccmServerInfo
    {
        public string ServerName { get; set; }
        public string SiteCode { get; set; }
        public string Role { get; set; }
        public string DistinguishedName { get; set; }
        
        public override string ToString()
        {
            if (!string.IsNullOrEmpty(SiteCode))
            {
                return $"{ServerName} (Site: {SiteCode}, Role: {Role})";
            }
            return $"{ServerName} (Role: {Role})";
        }
    }
}