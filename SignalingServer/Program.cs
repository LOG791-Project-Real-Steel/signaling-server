using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseWebSockets();

var clients = new Dictionary<string, WebSocket?>()
{
    { "robot", null },
    { "oculus", null }
};

var messageQueue = new Dictionary<string, ConcurrentQueue<string>>()
{
    { "robot", new ConcurrentQueue<string>() },
    { "oculus", new ConcurrentQueue<string>() }
};

app.Map("/robot/signaling", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("WebSocket requests only");
        return;
    }

    var role = context.Request.Query["role"].ToString();
    if (role != "robot" && role != "oculus")
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("Missing or invalid role (must be 'robot' or 'oculus')");
        return;
    }

    var ws = await context.WebSockets.AcceptWebSocketAsync();
    clients[role] = ws;

    Console.WriteLine($"{role} connected from {context.Connection.RemoteIpAddress}");
    Console.WriteLine($"Current clients: robot = {clients["robot"]?.State}, oculus = {clients["oculus"]?.State}");

    // Flush queued messages if peer already connected
    var other = role == "robot" ? "oculus" : "robot";
    while (messageQueue[role].TryDequeue(out var msg))
    {
        Console.WriteLine($"Flushing cached message to {role}: {msg}");
        await ws.SendAsync(Encoding.UTF8.GetBytes(msg), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    var buffer = new byte[8192];
    try
    {
        while (ws.State == WebSocketState.Open)
        {
            var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                break;
            }

            var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
            Console.WriteLine($"[{role}] -> {message}");

            if (clients[other] != null && clients[other]!.State == WebSocketState.Open)
            {
                await clients[other]!.SendAsync(Encoding.UTF8.GetBytes(message), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            else
            {
                Console.WriteLine($"Queuing message for {other}: {message}");
                messageQueue[other].Enqueue(message);
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error in {role} WebSocket: {ex.Message}");
    }
    finally
    {
        Console.WriteLine($"{role} disconnected.");
        clients[role] = null;
        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Bye", CancellationToken.None);
    }
});

app.Run("http://0.0.0.0:5000");
