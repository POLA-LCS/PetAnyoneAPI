#nullable enable

namespace PetAnyone;

/// <summary>
/// Describes how a registered NPC participates in the petting API. Registration itself makes the
/// NPC pettable. These flags control the world/UI affordances a consumer may build on top.
/// </summary>
public sealed class PetNpcDefinition(string? displayName = null, bool allowWorldPet = true, bool showChatButton = true, string? buttonText = null)
{
    /// <summary>Shared definition with all defaults enabled.</summary>
    public static readonly PetNpcDefinition Default = new();


    /// <summary>Optional display name override. Falls back to the NPC's own name.</summary>
    public string? DisplayName { get; } = displayName;

    /// <summary>Whether right-click world petting is allowed for this NPC.</summary>
    public bool AllowWorldPet { get; } = allowWorldPet;

    /// <summary>Whether the chat-button petting affordance should be shown for this NPC.</summary>
    public bool ShowChatButton { get; } = showChatButton;

    /// <summary>Chat button label for this NPC. Defaults to "pet &lt;3".</summary>
    public string ButtonText { get; } = string.IsNullOrWhiteSpace(buttonText) ? "pet <3" : buttonText;
}
