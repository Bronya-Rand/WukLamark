using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using WukLamark.Models;
using WukLamark.Windows.Components;

namespace WukLamark.Windows.Sections;

internal sealed class TableViewSection(MarkerTableComponent tableComponent)
{
    private readonly MarkerTableComponent tableComponent = tableComponent;

    public void Draw(List<Marker> filteredMarkers, int totalCount)
    {
        ImGui.Text($"Showing {filteredMarkers.Count} of {totalCount} markers");
        ImGui.Spacing();

        var avail = ImGui.GetContentRegionAvail();
        using (var child = ImRaii.Child("TableViewSectionChild", avail))
        {
            tableComponent.Draw(filteredMarkers);
        }
    }
}
