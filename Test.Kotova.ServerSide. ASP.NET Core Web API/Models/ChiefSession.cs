using System.Timers;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Models
{
    public class ChiefSession
    {
        public int ChiefId { get; private set; }
        private System.Timers.Timer timer;
        public bool IsChiefOnline { get; private set; } = true;

        public ChiefSession(int chiefId)
        {
            ChiefId = chiefId;
            // Timer setup: 60000 milliseconds = 1 minute
            timer = new System.Timers.Timer(60000);
            timer.Elapsed += CheckChiefActivity;
            timer.AutoReset = false;  // Only trigger once unless reset
            timer.Start();
        }


        public void ChiefPinged()
        {
            // Reset the timer each time a ping is received
            timer.Stop();
            timer.Start();
            IsChiefOnline = true;
        }

        private void CheckChiefActivity(object sender, ElapsedEventArgs e)
        {
            IsChiefOnline = false;
            // Additional logic when chief goes offline
        }

        public void EndSession()
        {
            timer.Stop();
            timer.Dispose();
        }
    }
}
