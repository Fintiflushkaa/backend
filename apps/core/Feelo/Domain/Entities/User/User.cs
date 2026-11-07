using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities.User;

[Table("Users")]
public sealed record User
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; init; } = Guid.NewGuid();
    
    public string Name { get; private set; }
    public string Email { get; private set; }
    public string PasswordHash { get; private set; }

    public SleepProfile? SleepProfile { get; private set; }
    public RhythmPreferences? RhythmPreferences { get; private set; }
    public PlanningSettings? PlanningSettings { get; private set; }

    public DateTimeOffset? OnboardingCompletedAt { get; private set; }

    public User(string Name, string Email, string passwordHash)
    {
        this.Name = Name;
        this.Email = Email;
        this.PasswordHash = passwordHash;
    }

    public void ApplySurveyAnswers(
        RhythmPreferences preferences,
        SleepProfile sleepProfile)
    {
        ArgumentNullException.ThrowIfNull(sleepProfile);
        ArgumentNullException.ThrowIfNull(preferences);

        SleepProfile = sleepProfile;
        RhythmPreferences = preferences;
    }
}