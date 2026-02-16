using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace RossvyazServer.Hubs
{
    public class ChatHub : Hub
    {
        // Текстовые сообщения
        public async Task SendMessage(string channel, string user, string text)
        {
            await Clients.Group(channel).SendAsync("ReceiveMessage", channel, user, text);
        }

        // Присоединиться к каналу
        public async Task JoinChannel(string channel, string user)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, channel);
            await Clients.Group(channel).SendAsync("SystemMessage", $"Пользователь {user} присоединился к {channel}");
        }

        // Выйти из канала
        public async Task LeaveChannel(string channel, string user)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, channel);
            await Clients.Group(channel).SendAsync("SystemMessage", $"Пользователь {user} покинул {channel}");
        }

        // Пересылка аудио-чанков (byte[]) — сервер ретранслирует нужной группе
        public async Task SendAudio(string channel, byte[] audioChunk)
        {
            await Clients.Group(channel).SendAsync("ReceiveAudio", Context.ConnectionId, audioChunk);
        }

        // Клиент может запросить свой ConnectionId
        public string GetConnectionId()
        {
            return Context.ConnectionId;
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // Можно уведомлять группы о выходе — TODO
            await base.OnDisconnectedAsync(exception);
        }
    }
}
