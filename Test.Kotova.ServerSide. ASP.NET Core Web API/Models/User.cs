﻿using System;
namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Models
{
    public class User //ITS FOR DATABASE
    {
        public int id { get; set; }
        public string username { get; set; }
        public string password_hash { get; set; }
        public int user_role { get; set; }
        public string? current_personnel_number { get; set; }
        public string? current_email { get; set; }
        public int department_id { get; set; }
        public string? desk_number { get; set; }
    }
    public class UserForAuthentication //ITS FOR AUTHENTICATION FROM BODY
    {
        public string username { get; set; }
        public string password { get; set;}
        public int time_for_being_authenticated { get; set; }

    }
}
