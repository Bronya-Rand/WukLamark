using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using WukLamark.Models;
using WukLamark.Utils;

namespace WukLamark.Windows.Components;

/// <summary>
/// Represents a popup dialog for editing marker properties.
/// </summary>
internal sealed class MarkerEditPopup(Plugin plugin)
{
    private readonly Plugin plugin = plugin;
    private readonly IconEditFields iconEditFields = new(plugin);

    #region Editing State
    private bool isOpen = false;
    private bool shouldOpen = false;
    private Marker? editingMarker;
    private MarkerGroup? editingMarkerGroup;

    private Guid? editingGroupId;
    private Guid? editingTemplateId;

    private string editingName = string.Empty;
    private string editingNote = string.Empty;
    private MarkerScope editingScope = MarkerScope.Personal;
    private bool editingReadOnly = false;
    private bool editingAppliesToAllWorlds = false;

    #endregion

    public Action<Marker, MarkerEditResult>? OnSave { get; set; }

    public void Open(Marker marker, MarkerGroup? markerGroup)
    {
        editingMarker = marker;
        editingMarkerGroup = markerGroup;
        LoadFromMarker(marker, markerGroup);
        isOpen = true;
        shouldOpen = true;
    }

    /// <summary>
    /// Loads editing state from the given marker.
    /// </summary>
    /// <remarks>Call this before opening the popup.</remarks>
    private void LoadFromMarker(Marker marker, MarkerGroup? markerGroup)
    {
        editingGroupId = markerGroup?.Id;
        editingTemplateId = marker.TemplateId;

        editingName = marker.Name;
        editingNote = marker.Notes;
        editingScope = marker.Scope;
        editingReadOnly = marker.IsReadOnly;
        editingAppliesToAllWorlds = marker.AppliesToAllWorlds;

        iconEditFields.LoadFrom(marker.Icon);
    }

    public void Draw()
    {
        if (!isOpen || editingMarker == null) return;

        var identifier = editingMarker.Id.ToString();
        var popupId = $"EditMarker##{identifier}";

        if (shouldOpen)
        {
            ImGui.OpenPopup(popupId);
            shouldOpen = false;
        }

        using var editMarkerPopup = ImRaii.Popup(popupId);
        if (!editMarkerPopup)
        {
            if (!ImGui.IsPopupOpen(popupId))
            {
                isOpen = false;
                editingMarker = null;
                editingMarkerGroup = null;
            }
            return;
        }

        var currentHash = plugin.MarkerStorageService.CurrentCharacterHash;
        var isMarkerCreator = editingMarker.CharacterHash != null &&
                               currentHash != null &&
                               editingMarker.CharacterHash == currentHash;

        // Inherit scope from group if marker is in a group
        var isGrouped = editingMarkerGroup != null;
        var effectiveScope = isGrouped ? editingMarkerGroup!.Scope : editingMarker.Scope;

        if (isGrouped)
            editingScope = editingMarkerGroup!.Scope;

        var selectedScope = isGrouped ? editingMarkerGroup!.Scope : editingScope;
        var canOpenEdit = false;
        var cannotEditReason = string.Empty;

        // Validate edit permissions before rendering any fields
        if (isGrouped)
        {
            // Validate group permissions
            var isGroupCreator = editingMarkerGroup!.CreatorHash != null &&
                                 currentHash != null &&
                                 editingMarkerGroup.CreatorHash == currentHash;

            if (editingMarkerGroup.Scope == MarkerScope.Personal)
            {
                canOpenEdit = isGroupCreator;
                if (!canOpenEdit)
                    cannotEditReason = "Only the group's creator can edit markers in this personal group.";
            }
            else
            {
                if (editingMarkerGroup.IsReadOnly)
                {
                    canOpenEdit = isMarkerCreator;
                    if (!canOpenEdit)
                        cannotEditReason = "This group is read-only and only the marker creator can edit it.";
                }
                else
                {
                    canOpenEdit = !editingMarker.IsReadOnly || isMarkerCreator;
                    if (!canOpenEdit)
                        cannotEditReason = $"'{editingMarker.Name}' is read-only and cannot be edited by non-creators";
                }
            }
        }
        else
        {
            // Validate individual marker permissions
            canOpenEdit = (effectiveScope == MarkerScope.Personal && isMarkerCreator) ||
                          (effectiveScope == MarkerScope.Shared && (!editingMarker.IsReadOnly || isMarkerCreator));

            if (!canOpenEdit)
            {
                cannotEditReason = effectiveScope == MarkerScope.Personal
                    ? $"Only the creator can edit '{editingMarker.Name}'."
                    : $"'{editingMarker.Name}' is read-only and cannot be edited.";
            }
        }

        // Exit early if user lacks permissions
        if (!canOpenEdit)
        {
            Plugin.Log.Warning($"Edit popup opened without permission for marker '{editingMarker.Id}'. {cannotEditReason}.");
            ImGui.CloseCurrentPopup();
            return;
        }

        var inheritedReadOnly = isGrouped && editingMarkerGroup!.Scope == MarkerScope.Shared && editingMarkerGroup.IsReadOnly;
        var isSharedReadOnly = selectedScope == MarkerScope.Shared && (editingReadOnly || inheritedReadOnly);
        var canEditGeneralFields = !isSharedReadOnly;
        var canEditScope = !isGrouped && isMarkerCreator && !editingReadOnly;
        var canEditReadOnly = selectedScope == MarkerScope.Shared && isMarkerCreator && !editingName.IsNullOrEmpty();

        var canSave = !editingName.IsNullOrEmpty();
        // Saving only enabled if read-only state is false and is creator
        if (selectedScope == MarkerScope.Shared && editingMarker.IsReadOnly)
            canSave = isMarkerCreator && editingReadOnly != editingMarker.IsReadOnly;

        ImGui.Text("Edit Marker");
        ImGui.Separator();

        IconEditFields.DrawNameField(identifier, ref editingName, !canEditGeneralFields);

        var templates = plugin.MarkerStorageService.GetTemplates();
        editingTemplateId = IconEditFields.DrawTemplatePicker(identifier, editingTemplateId, plugin.Configuration, templates, !canEditGeneralFields);

        var isTemplateAssigned = editingTemplateId != null;
        var disableTemplateFields = !canEditGeneralFields || isTemplateAssigned;

        // When a template is assigned, we disable the individual fields. 
        // The map renderer will read from the template instead of these local fields via GetEffective methods.

        iconEditFields.Draw(identifier, editingMarker.Name, disableTemplateFields);

        // Group assignment dropdown
        var groups = plugin.MarkerStorageService.GetVisibleGroups();
        editingGroupId = IconEditFields.DrawGroupPicker(identifier, editingGroupId, groups, currentHash, disableTemplateFields);

        IconEditFields.DrawNotesField(identifier, ref editingNote, !canEditGeneralFields);

        // Scope dropdown
        var scopeTooltip = isGrouped
            ? "Marker scope is inherited from its group."
            : !isMarkerCreator
                ? "Only the creator can change scope."
                : editingReadOnly && selectedScope == MarkerScope.Shared
                    ? "Disable read-only before changing scope."
                    : "Sets the visibility of the marker to other characters on the same PC.\nPersonal markers are only visible to you, while shared markers are visible to any character that logs in to FFXIV from this PC.";
        editingScope = IconEditFields.DrawScopePicker(identifier, editingScope, !canEditScope || isTemplateAssigned, scopeTooltip);

        using (ImRaii.Disabled(disableTemplateFields))
            ImGui.Checkbox($"Visible Crossworld###AllWorlds{identifier}", ref editingAppliesToAllWorlds);
        if (ImWuk.IsItemHoveredWhenDisabled())
            ImGui.SetTooltip("When enabled, this marker appears on matching maps in all worlds/data centers.");

        // Read-only checkbox (only for shared markers and only editable by the creator)
        if (selectedScope == MarkerScope.Shared)
        {
            ImGui.Spacing();
            using (ImRaii.Disabled(!canEditReadOnly))
            {
                ImGui.Checkbox("Read-Only###MarkerReadOnly", ref editingReadOnly);
            }
            if (ImWuk.IsItemHoveredWhenDisabled())
            {
                var tooltip = !isMarkerCreator ?
                    "Only the creator can set this marker to read-only." :
                    editingName.IsNullOrEmpty() ? "Markers must have a name before they can be set to read-only." :
                    "When enabled, all fields are locked and marker deletion is blocked. Only the creator can disable read-only.";
                ImGui.SetTooltip(tooltip);
            }
        }

        ImGui.Spacing();

        using (ImRaii.Disabled(!canSave))
            if (ImGui.Button("Save###EditMarkerSaveButton"))
            {
                var result = new MarkerEditResult
                {
                    Name = editingName,
                    Notes = editingNote,
                    GroupId = editingGroupId,
                    TemplateId = editingTemplateId,
                    Scope = isGrouped ? editingMarkerGroup!.Scope : editingScope,
                    IsReadOnly = selectedScope == MarkerScope.Shared && editingReadOnly,
                    AppliesToAllWorlds = editingAppliesToAllWorlds,
                    Icon = iconEditFields.ToMarkerIcon()
                };
                OnSave?.Invoke(editingMarker, result);

                isOpen = false;
                editingMarker = null;
                editingMarkerGroup = null;
                ImGui.CloseCurrentPopup();
            }

        ImGui.SameLine();

        if (ImGui.Button("Cancel###EditMarkerCancel"))
        {
            isOpen = false;
            editingMarker = null;
            editingMarkerGroup = null;
            ImGui.CloseCurrentPopup();
        }
    }
}

/// <summary>
/// Represents the edited values from a marker edit session.
/// </summary>
public sealed class MarkerEditResult : IEditableMarkerResult
{
    public string Name { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
    public Guid? GroupId { get; init; }
    public Guid? TemplateId { get; init; }
    public required MarkerIcon Icon { get; init; }
    public MarkerScope Scope { get; init; }
    public bool IsReadOnly { get; init; }
    public bool AppliesToAllWorlds { get; init; }
}
