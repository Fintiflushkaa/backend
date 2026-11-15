namespace App.Synchronization;

public enum TaskSyncStatus
{
    Created,
    Deleted,
    Modified,
    Synced,
    Conflicted,
    NotPlanned
}