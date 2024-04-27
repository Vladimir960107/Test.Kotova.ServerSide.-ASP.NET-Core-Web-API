using System;
namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Models
{
    public class User //ITS FOR DATABASE
    {
        public int id { get; set; }
        public string username { get; set; }
        public string password_hash { get; set; }

    }
}
