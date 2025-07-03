using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using System.Net.WebSockets;
using System.Text;
using System.Threading;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

WebSocket? oculusSocket = null;
WebSocket? robotSocket = null;

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

    Console.WriteLine($"WebSocket connected from {remoteIP}");

    // Assign socket role
    if (oculusSocket == null)
    {
        oculusSocket = socket;
        Console.WriteLine("Assigned Oculus socket.");
    }
    else if (robotSocket == null)
    {
        robotSocket = socket;
        Console.WriteLine("Assigned Robot socket.");
    }
    else
    {
        Console.WriteLine("Too many connections.");
        return;
    }

    var buffer = new byte[8192];
    while (socket.State == WebSocketState.Open)
    {
        var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        if (result.MessageType == WebSocketMessageType.Close)
        {
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None);
            break;
        }

        var msg = new ArraySegment<byte>(buffer, 0, result.Count);
        WebSocket? target = (socket == oculusSocket) ? robotSocket : oculusSocket;

        if (target != null && target.State == WebSocketState.Open)
        {
            await target.SendAsync(msg, WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }

    if (socket == oculusSocket) oculusSocket = null;
    if (socket == robotSocket) robotSocket = null;

    Console.WriteLine("WebSocket disconnected.");
});

app.Run("http://0.0.0.0:5000");