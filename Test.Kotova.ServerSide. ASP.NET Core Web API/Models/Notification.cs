using System.Reflection.Metadata.Ecma335;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Models
{
    public class NotificationFromDB
    {
        public int instruction_id { get; set; }
        public bool is_instruction_passed { get; set; }
        public DateTime? date_when_passed { get; set; }
        public DateTime when_was_sent_to_user { get; set; }
        public DateTime when_was_sent_UTC_Time { get; set; }

    }
}
