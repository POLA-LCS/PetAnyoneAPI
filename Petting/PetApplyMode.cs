namespace PetAnyone;

/// <summary>How an application is intended: one tap or one hold refresh.</summary>
public enum PetApplyMode : byte
{
    /// <summary>A single application that starts and finishes on its own.</summary>
    Tap,
    /// <summary>A sustained hold that refreshes an open session.</summary>
    Hold
}
