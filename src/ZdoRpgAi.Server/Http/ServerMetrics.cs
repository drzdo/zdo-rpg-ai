using System.Globalization;
using System.Text;

namespace ZdoRpgAi.Server.Http;

public enum WebSocketRejectionReason {
    NotWebSocket,
    BadToken
}

public static class ServerMetrics {
    private static long _webSocketConnectionsTotal;
    private static long _webSocketRejectionsNotWebSocketTotal;
    private static long _webSocketRejectionsBadTokenTotal;

    public static void RecordWebSocketAccepted() => Interlocked.Increment(ref _webSocketConnectionsTotal);

    public static void RecordWebSocketRejected(WebSocketRejectionReason reason) {
        switch (reason) {
            case WebSocketRejectionReason.NotWebSocket:
                Interlocked.Increment(ref _webSocketRejectionsNotWebSocketTotal);
                break;
            case WebSocketRejectionReason.BadToken:
                Interlocked.Increment(ref _webSocketRejectionsBadTokenTotal);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(reason), reason, null);
        }
    }

    public static string RenderPrometheusText(int activeWebSocketConnections) {
        var builder = new StringBuilder();

        AppendMetricHeader(
            builder,
            "zdo_rpg_ai_active_websocket_connections",
            "Current number of active websocket client connections.",
            "gauge");
        AppendSample(builder, "zdo_rpg_ai_active_websocket_connections", activeWebSocketConnections);

        AppendMetricHeader(
            builder,
            "zdo_rpg_ai_websocket_connections_total",
            "Total number of accepted websocket client connections.",
            "counter");
        AppendSample(
            builder,
            "zdo_rpg_ai_websocket_connections_total",
            Volatile.Read(ref _webSocketConnectionsTotal));

        AppendMetricHeader(
            builder,
            "zdo_rpg_ai_websocket_rejections_total",
            "Total number of rejected websocket upgrade requests.",
            "counter");
        AppendLabeledSample(
            builder,
            "zdo_rpg_ai_websocket_rejections_total",
            "reason",
            "not_websocket",
            Volatile.Read(ref _webSocketRejectionsNotWebSocketTotal));
        AppendLabeledSample(
            builder,
            "zdo_rpg_ai_websocket_rejections_total",
            "reason",
            "bad_token",
            Volatile.Read(ref _webSocketRejectionsBadTokenTotal));

        return builder.ToString();
    }

    private static void AppendMetricHeader(StringBuilder builder, string name, string help, string type) {
        builder.Append("# HELP ").Append(name).Append(' ').Append(help).Append('\n');
        builder.Append("# TYPE ").Append(name).Append(' ').Append(type).Append('\n');
    }

    private static void AppendSample(StringBuilder builder, string name, long value) {
        builder.Append(name)
            .Append(' ')
            .Append(value.ToString(CultureInfo.InvariantCulture))
            .Append('\n');
    }

    private static void AppendLabeledSample(
        StringBuilder builder,
        string name,
        string labelName,
        string labelValue,
        long value) {
        builder.Append(name)
            .Append('{')
            .Append(labelName)
            .Append("=\"")
            .Append(EscapeLabelValue(labelValue))
            .Append("\"} ")
            .Append(value.ToString(CultureInfo.InvariantCulture))
            .Append('\n');
    }

    private static string EscapeLabelValue(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal);

}