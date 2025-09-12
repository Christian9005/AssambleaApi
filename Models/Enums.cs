namespace AssambleaApi.Models;

public enum MeetingStatus
{
    Created,
    Started,
    Registration,
    ClosingInterventions,
    OpeningInterventions,
    FirstVoting,
    SecondVoting,
    CountingVotes,
    Closed
}

public enum VoteOption
{
    Yes,
    No,
    Blank,
    Abstention
}
