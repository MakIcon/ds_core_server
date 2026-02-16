# Rossvyaz (sample)

Минимальный репозиторий с примером сервера SignalR и клиента WPF для локального теста.

## Структура
- server/  - ASP.NET Core SignalR hub
- client/  - WPF client (uses SignalR.Client + NAudio)

## Быстрый запуск (локально)
### Сервер
```bash
cd server
dotnet restore
dotnet build
dotnet run --urls "http://0.0.0.0:5000"
```

### Клиент (WPF)
- Откройте проект `client/RossvyazClient.sln` или соберите:
```bash
cd client
dotnet restore
dotnet build
```
- В `MainWindow.xaml.cs` поправьте URL хаба: `var url = "http://<ubuntu-ip>:5000/chatHub";`

## Примечания
- Для локальных тестов можно использовать HTTP. Для сетевого/публичного доступа — добавьте HTTPS/NGINX и CORS.
- Это минимальный пример для проверки архитектуры. Для продакшна нужны безопасность, авторизация и улучшенная обработка аудио.
