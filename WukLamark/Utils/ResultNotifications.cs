using System.Numerics;
using Dalamud.Interface.Colors;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Utility;

namespace WukLamark.Utils
{
    public enum MessageType
    {
        Success,
        Error,
        Warning
    }
    public static class ResultNotifications
    {
        private static Dalamud.Game.Text.SeStringHandling.SeString BuildChatMessage(string message, Vector4 color, bool omitPluginName = false)
        {
            var builder = new Lumina.Text.SeStringBuilder()
                .PushColorRgba(DalamudVector4ToLuminaVector4(color))
                .Append(omitPluginName ? message : $"[${Plugin.Name}] {message}")
                .PopColor()
                .ToReadOnlySeString();
            return builder.ToDalamudString();
        }
        private static Notification BuildDalamudNotification(string message, NotificationType type) =>
            new()
            { Content = message, Type = type };
        public static void SendMessage(string message, MessageType messageType, bool omitPluginName = false, bool sendBoth = false)
        {
            // Check login state
            var isLoggedIn = Plugin.ClientState.IsLoggedIn;

            // Send to ChatGui if logged in
            if (isLoggedIn)
            {
                var chatMessage = messageType switch
                {
                    MessageType.Success => BuildChatMessage(message, ImGuiColors.SuccessForeground, omitPluginName),
                    MessageType.Error => BuildChatMessage(message, ImGuiColors.ErrorForeground, omitPluginName),
                    MessageType.Warning => BuildChatMessage(message, ImGuiColors.WarningForeground, omitPluginName),
                    _ => null,
                };
                if (chatMessage == null)
                    Plugin.Log.Error($"Failed to build chat message for type {messageType}");
                else
                    Plugin.ChatGui.Print(chatMessage);
            }

            // Send as notification if not logged in or if sendBoth is true
            if (!isLoggedIn || sendBoth)
            {
                var notificationType = messageType switch
                {
                    MessageType.Success => NotificationType.Success,
                    MessageType.Error => NotificationType.Error,
                    MessageType.Warning => NotificationType.Warning,
                    _ => NotificationType.Info
                };
                var notification = BuildDalamudNotification(message, notificationType);
                Plugin.NotificationManager.AddNotification(notification);
            }
        }

        #region Helpers
        /// <summary>
        /// Converts a Dalamud ImGuiColors <see cref="Vector4"/> to a Lumina-compatible RGBA color struct.
        /// </summary>
        /// <param name="dalamudColor">The Dalamud color to convert.</param>
        /// <returns>A Vector4 of the same color in byte-form.</returns>
        private static Vector4 DalamudVector4ToLuminaVector4(Vector4 dalamudColor) =>
            new(
                (byte)(dalamudColor.X * 255),
                (byte)(dalamudColor.Y * 255),
                (byte)(dalamudColor.Z * 255),
                (byte)(dalamudColor.W * 255)
            );
        #endregion
    }
}
