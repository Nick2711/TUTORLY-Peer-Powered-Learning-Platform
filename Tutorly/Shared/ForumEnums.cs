namespace Tutorly.Shared
{
    public enum PostType
    {
        Question,
        Discussion,
        Resource
    }

    public enum CommunityType
    {
        Course,
        General,
        StudyGroup
    }

    public enum VoteType
    {
        Downvote = -1,
        Upvote = 1
    }

    public enum PostSortBy
    {
        Hot,
        New,
        Top
    }

    public enum CommunitySortBy
    {
        Activity,
        Alphabetical,
        Members
    }
}

