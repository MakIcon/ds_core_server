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
