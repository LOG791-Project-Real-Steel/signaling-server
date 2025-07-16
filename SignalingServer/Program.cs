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

app.UseWebSockets();

app.Map("/signaling", async context =>
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

app.Run("http://0.0.0.0:5000");