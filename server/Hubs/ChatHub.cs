using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using RossvyazServer.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Добавляем SignalR с настройками для передачи аудио (без ограничений по размеру)
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
    options.MaximumReceiveMessageSize = null;
});

// Настройка CORS: разрешаем подключения с любого адреса для локальных тестов
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Важно для работы за Nginx: корректная обработка заголовков прокси
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseCors();

// Маршруты
app.MapGet("/", () => "SignalR ChatHub running via Nginx");
app.MapHub<ChatHub>("/chatHub");

app.Run();
gool@gool-Legion-5-15ACH6H:~/ds_core_server/server$ cat Hubs/ChatHub.cs 
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace RossvyazServer.Hubs
{
    public class ChatHub : Hub
    {
        // Хранилище: ID соединения -> Имя пользователя
        private static readonly ConcurrentDictionary<string, string> _users = new();

        // Присоединиться к каналу
        public async Task JoinChannel(string channel, string user)
        {
            // Сохраняем имя пользователя для этого ID
            _users[Context.ConnectionId] = user;

            await Groups.AddToGroupAsync(Context.ConnectionId, channel);

            // Уведомляем всех в канале, что зашел новый участник (для списка "Личный состав")
            await Clients.Group(channel).SendAsync("UserJoined", channel, user, Context.ConnectionId);

            // Отправляем системное сообщение в чат
            await Clients.Group(channel).SendAsync("SystemMessage", $"Пользователь {user} вошел в спецсвязь.");
        }

        // Пересылка аудио-чанков
        public async Task SendAudio(string channel, byte[] audioChunk)
        {
            // Отправляем аудио всем в группе, КРОМЕ отправителя (OthersInGroup), 
            // чтобы вы не слышали сами себя с задержкой
            await Clients.OthersInGroup(channel).SendAsync("ReceiveAudio", Context.ConnectionId, audioChunk);
        }

        // Текстовые сообщения
        public async Task SendMessage(string channel, string user, string text)
        {
            await Clients.Group(channel).SendAsync("ReceiveMessage", channel, user, text);
        }

        // Выйти из канала
        public async Task LeaveChannel(string channel, string user)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, channel);
            await Clients.Group(channel).SendAsync("UserLeft", Context.ConnectionId);
        }

        // Клиент запрашивает свой ID (нужно для логики подсветки самого себя)
        public string GetConnectionId()
        {
            return Context.ConnectionId;
        }

        // Обработка разрыва соединения
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (_users.TryRemove(Context.ConnectionId, out var userName))
            {
                // Сообщаем всем, что пользователь отключился, чтобы убрать его из списка в UI
                await Clients.All.SendAsync("UserLeft", Context.ConnectionId);
            }
            await base.OnDisconnectedAsync(exception);
        }
    }
}
