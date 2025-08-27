namespace SCML.Models
{
    /// <summary>
    /// Credentials model for authentication
    /// </summary>
    public class Credentials
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string Domain { get; set; }
        public string Address { get; set; }
    }
}