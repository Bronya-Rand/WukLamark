using System;
using WukLamark.Services;

namespace WukLamark.Render
{
    /// <summary>
    /// Boilerplate interface for map marker renderers
    /// </summary>
    internal interface IMapMarkerRender : IDisposable
    {
        bool IsEnabled { get; }
        void BeginRender();
        void RenderMarker(uint selectedMapId, float uiScale, MarkerMapRenderData markerInfo);
        void EndRender();
    }
}
