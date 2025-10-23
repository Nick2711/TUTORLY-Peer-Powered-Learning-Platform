namespace Tutorly.Shared
{
    public enum NotificationPreference
    {
        Email,
        Sms,
        Whatsapp,
    }

    public class Tutor : User, INotification
    {
        public NotificationPreference Preference { get; set; }

        public Tutor(bool active, int userId, string email, string name, string password, RoleType role)
            : base(active, userId, email, name, password, role) { }

        public void Notify(Topic topic)
        {
            var message = $"{topic.TopicName} created by {topic.CreatedBy}";
            switch (Preference)
            {
                case NotificationPreference.Email: sendEmail(message); break;
                case NotificationPreference.Sms: sendSms(message); break;
                case NotificationPreference.Whatsapp: sendWhatsapp(message); break;
            }
        }

        public void sendEmail(string message) { /* integrate mail service */ }
        public void sendSms(string message) { /* integrate sms service */ }
        public void sendWhatsapp(string message) { /* integrate WhatsApp API */ }
    }

}