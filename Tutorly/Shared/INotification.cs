namespace Tutorly.Shared
{
    public interface INotification
    {
        public void sendInAppNotif(string message) { }
        public void sendEmail(string message) { /*API call*/}
        public void sendSms(string message) { }
        public void sendWhatsapp(string message) { }
    }
}
