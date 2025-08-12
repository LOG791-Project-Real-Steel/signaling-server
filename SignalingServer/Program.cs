using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

List<WebSocket> webSockets = [];
WebSocket? unitySocket = null;
WebSocket? clientSocket = null;

app.UseWebSockets();
app.MapStaticAssets();

app.MapGet("/robot/client", () => Results.File("~/index.html", "text/html"));
app.MapGet("/robot/client2", () => Results.File("~/index2.html", "text/html"));

app.Map("/robot/signaling", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    var socket = await context.WebSockets.AcceptWebSocketAsync();
    var remoteIP = context.Connection.RemoteIpAddress?.ToString();

    webSockets.Add(socket);

    Console.WriteLine($"WebSocket connected from {remoteIP}");

    var buffer = new byte[8192];
    try
    {
        while (socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None);
                break;
            }

            var msg = new ArraySegment<byte>(buffer, 0, result.Count);
            foreach (WebSocket ws in webSockets)
            {
                if (ws.State == WebSocketState.Open && ws != socket)
                {
                    await ws.SendAsync(msg, result.MessageType, result.EndOfMessage, CancellationToken.None);
                }
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
        return;
    }
    finally
    {
        socket.Dispose();
        webSockets.Remove(socket);
    }

    Console.WriteLine("WebSocket disconnected.");
});

app.Map("/robot/signaling2", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    var socket = await context.WebSockets.AcceptWebSocketAsync();
    Console.WriteLine("WebSocket connected from " + context.Connection.RemoteIpAddress);

    var buffer = new byte[8192];
    WebSocketRole role = WebSocketRole.Unknown;

    try
    {
        while (socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                break;
            }

            var msgStr = Encoding.UTF8.GetString(buffer, 0, result.Count);
            Console.WriteLine($"[{role}] ➜ {msgStr}");

            // Identify role
            if (msgStr.Contains("\"role\":"))
            {
                if (msgStr.Contains("unity"))
                {
                    unitySocket = socket;
                    role = WebSocketRole.Unity;
                    Console.WriteLine("Registered as Unity");
                }
                else if (msgStr.Contains("client"))
                {
                    clientSocket = socket;
                    role = WebSocketRole.Client;
                    Console.WriteLine("Registered as Client");
                }

                continue;
            }

            // Relay signaling messages
            WebSocket? target = null;

            if (msgStr.Contains("\"to\":\"unity\"") && unitySocket != null && unitySocket.State == WebSocketState.Open)
            {
                target = unitySocket;
            }
            else if (msgStr.Contains("\"to\":\"robot\"") && clientSocket != null && clientSocket.State == WebSocketState.Open)
            {
                target = clientSocket;
            }
            else
            {
                // Fallback: infer from role
                if (role == WebSocketRole.Unity && clientSocket != null && clientSocket.State == WebSocketState.Open)
                    target = clientSocket;
                else if (role == WebSocketRole.Client && unitySocket != null && unitySocket.State == WebSocketState.Open)
                    target = unitySocket;
            }

            if (target != null)
            {
                var msgBytes = Encoding.UTF8.GetBytes(msgStr);
                await target.SendAsync(new ArraySegment<byte>(msgBytes), WebSocketMessageType.Text, true, CancellationToken.None);
                Console.WriteLine("Forwarded message.");
            }
            else
            {
                Console.WriteLine("No valid target to forward message.");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("Error: " + ex.Message);
    }
    finally
    {
        socket.Dispose();

        if (unitySocket == socket) unitySocket = null;
        if (clientSocket == socket) clientSocket = null;

        Console.WriteLine("WebSocket closed.");
    }
});

app.Run("http://0.0.0.0:5000");

enum WebSocketRole
{
    Unknown,
    Unity,
    Client
}
