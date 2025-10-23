namespace Tutorly.Shared
{
    public enum ConversationType
    {
        Direct,
        Group
    }

    public enum MessageType
    {
        Text,
        File,
        Image,
        System,
        Announcement
    }

    public enum ConversationRole
    {
        Owner,
        Admin,
        Member
    }

    public enum InvitationStatus
    {
        Pending,
        Accepted,
        Declined
    }

    public enum PresenceStatus
    {
        Online,
        Away,
        Offline
    }
    public enum EscalationType
    {
        ModuleTutor,
        Admin
    }
}

