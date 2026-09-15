namespace PetAnyone;

/// <summary>
/// Describes how a registered NPC participates in the petting API. Registration itself makes the
/// NPC pettable; these flags control the world/UI affordances a consumer may build on top.
/// </summary>
public sealed class PetNpcDefinition
{
    /// <summary>Shared definition with all defaults enabled.</summary>
    public static readonly PetNpcDefinition Default = new();

    public PetNpcDefinition(string? displayName = null, bool allowWorldPet = true, bool showChatButton = true, string? buttonText = null)
    {
        DisplayName = displayName;
        AllowWorldPet = allowWorldPet;
        ShowChatButton = showChatButton;
        ButtonText = string.IsNullOrWhiteSpace(buttonText) ? "pet <3" : buttonText;
    }

    /// <summary>Optional display name override; falls back to the NPC's own name.</summary>
    public string? DisplayName { get; }

    /// <summary>Whether right-click world petting is allowed for this NPC.</summary>
    public bool AllowWorldPet { get; }

    /// <summary>Whether the chat-button petting affordance should be shown for this NPC.</summary>
    public bool ShowChatButton { get; }

    /// <summary>Chat button label for this NPC. Defaults to "pet &lt;3".</summary>
    public string ButtonText { get; }
}
