using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Server
{
    internal class Program
    {
        private static Dictionary<string, string> clientsIdentity;
        private static ConcurrentDictionary<TcpClient, List<string>> clientsQuotes = new ConcurrentDictionary<TcpClient, List<string>>();
        private static readonly object lockObject = new object();
        const int port = 8080;
        static IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), port);
        static TcpListener listener;
        private static int currentClientCount = 0;
        private static readonly object countLockObject = new object();
        static int maxClientCount = 3;

        static async Task Main(string[] args)
        {
            listener = new TcpListener(ipEndPoint);
            listener.Start();
            InitializeClients();
            Console.WriteLine("Сервер запущен!");
            
            try
            {
                while (true)
                {
                    TcpClient client = await listener.AcceptTcpClientAsync();

                    if (await TakeClientCredentials(client))
                    {
                        if (CanAcceptClient())
                        {
                            Log($"Присоединился клиент: {client.Client.RemoteEndPoint}, {DateTime.Now}");
                            await WriteMessageAsync(client, "Hello");

                            IncrementClientCount();

                            _ = OneClientProcess(client);
                        }
                        else
                        {
                            await WriteMessageAsync(client, "Later");
                            Console.WriteLine("Достигнуто максимальное количество клиентов. Отклонено подключение нового клиента.");
                            client.Close();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Возникла ошибка: {ex.Message}");
            }
        }

        private static void InitializeClients()
        {
            clientsIdentity = new Dictionary<string, string>()
            {
                {"user1", "password1" },
                {"user2", "password2" },
                {"user3", "password3" }
            };
        }

        private static bool ValidateClient(string login, string pasword)
        {
            if (clientsIdentity.TryGetValue(login, out var currentPassword))
            {
                return pasword == currentPassword;
            }
            return false;
        }

        private static async Task< bool> TakeClientCredentials(TcpClient client)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                byte[] buffer = new byte[1024]; 
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                string clientCredentials = Encoding.Unicode.GetString(buffer, 0, bytesRead);
               //Cо STREAMREADER работать не хочет!
                //using StreamReader reader = new StreamReader(stream);
                //string clientCredentials = await reader.ReadLineAsync();
                if (clientCredentials != null)
                {
                    string[] credentialsArray = clientCredentials.Split(',');
                    string login = credentialsArray[0];
                    string password = credentialsArray[1];
                    if (ValidateClient(login, password))
                    {
                        return true;
                    }
                    else
                    {
                        byte[] data = Encoding.Unicode.GetBytes("InvalidCredentials");
                        await stream.WriteAsync(data, 0, data.Length);
                        Log($"Ошибка подключения: Неверные учетные данные от клиента {client.Client.RemoteEndPoint}");
                        client.Close();
                        return false;
                    }
                }
                else return false;
                
            }
            catch (Exception ex)
            {
                // Обработка возможных исключений
                Log($"Ошибка при обработке учетных данных клиента: {ex.Message}");
                return false;
            }
            
        }

        // Метод для проверки, можно ли принять нового клиента
        private static bool CanAcceptClient()
        {
            lock (countLockObject)
            {
                return currentClientCount < maxClientCount;
            }
        }

        // Метод для увеличения счетчика активных клиентов
        private static void IncrementClientCount()
        {
            lock (countLockObject)
            {
                currentClientCount++;
            }
        }

        // Метод для уменьшения счетчика активных клиентов
        private static void DecrementClientCount()
        {
            lock (countLockObject)
            {
                currentClientCount--;
            }
        }

        public static void InitializeQuotes(TcpClient client)
        {
            lock (lockObject)
            {
                clientsQuotes.TryAdd(client, new List<string>
                    {
                        "«Цели никогда не должны быть простыми. Они должны быть неудобными, чтобы заставить вас работать», — Майкл Фелпс",
                "«Возраст — это всего лишь ограничение, которое вы кладёте себе в голову», — Джеки Джойнер-Керси",
                "«Не бойтесь неудач, потому что это ваш путь к успеху», — Леброн Джеймс",
                "«Тело может многое выдержать. Вам нужно только убедить своё сознание в этом», — Эндрю Мерфи",
                "«Секрет жизни в том, чтобы семь раз упасть, но восемь раз подняться», — Пауло Коэльо",
                "«Неудача — это просто возможность начать снова, но уже более мудро», — Генри Форд",
                "«Величайшая слава в жизни заключается не в том, чтобы никогда не падать, а в том, чтобы подниматься каждый раз, когда мы падаем», — Ральф Уолдо Эмерсон",
                "«Успех — не окончателен, провал — не фатален: имеет значение лишь смелость продолжить путь», — Уинстон Черчилль",
                "«Вчера я был умным, и поэтому я хотел изменить мир. Сегодня я стал мудрым, и поэтому я меняю себя», — Джалаладдин Руми",
                " «Если мы не меняемся, мы не развиваемся. А если не развиваемся, то и не живём по-настоящему», — Гейл Шихи",
                "«Нам не дано вернуть вчерашний день, но то, что будет завтра, зависит от нас», — Линдон Джонсон, 36-й президент США",
                "«Никогда не поздно или — в моём случае — никогда не рано стать тем, кем ты хочешь стать. Временных рамок нет, можешь начать когда угодно. Можешь измениться или остаться прежним — правил не существует», — Фрэнсис Скотт Фицджеральд"
                    });
            }
        }
        public static async Task OneClientProcess(TcpClient client)
        {
            NetworkStream stream = null;
            try
            {
                stream = client.GetStream();


                InitializeQuotes(client);

                byte[] data = new byte[64];
                while (client.Connected)
                {
                    StringBuilder builder = new StringBuilder();
                    int bytes = 0;
                    do
                    {
                        bytes = await stream.ReadAsync(data, 0, data.Length);
                        builder.Append(Encoding.Unicode.GetString(data, 0, bytes));
                    } while (stream.DataAvailable);

                    string message = builder.ToString();
                    Console.WriteLine($"Клиент: {message}");

                    if (message.ToLower() == "bye")
                    {
                        Log($"Клиент {client.Client.RemoteEndPoint} отключился в: {DateTime.Now}");
                        DecrementClientCount();
                        break;
                    }

                    string response = GetComputerResponse(client);
                    data = Encoding.Unicode.GetBytes(response);
                    await stream.WriteAsync(data, 0, data.Length);

                    Log($"Цитата: {response}");

                    if (response == "bye")
                    {
                        lock (lockObject)
                        {
                            clientsQuotes.TryRemove(client, out _);
                        }
                        Console.WriteLine("Соединение разорвано сервером из-за окончания цитат и направления сообщения \"bye\"");
                        Log($"Клиент {client.Client.RemoteEndPoint} отключился в: {DateTime.Now}");
                        DecrementClientCount();
                        if (stream != null)
                            client.Close();
                        if (client != null)
                            client.Close();

                        break;
                    }
                }



            }
            catch (ObjectDisposedException)
            {
                Console.WriteLine("Сокет был закрыт");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Возникла ошибка: {ex.Message}");
            }
            finally
            {
                lock (lockObject)
                {
                    clientsQuotes.TryRemove(client, out _);
                }

                if (client != null && client.Connected)
                {
                    client.Close();
                }
            }
        }

        public static string GetComputerResponse(TcpClient client)
        {
            lock (lockObject)
            {
                if (clientsQuotes.TryGetValue(client, out var responses) && responses.Any())
                {
                    Random random = new Random();
                    int index = random.Next(responses.Count);
                    string quote = responses[index];
                    responses.RemoveAt(index);

                    return quote;
                }
                else
                {
                    return "bye";
                }
            }
        }

        public static void Log(string message)
        {
            lock (lockObject)
            {
                Console.WriteLine(message);
                // логируйте в файл или другие места по необходимости
            }
        }
        public static async Task WriteMessageAsync(TcpClient client, string message)
        {
            NetworkStream stream = client.GetStream();
            byte[] data = Encoding.Unicode.GetBytes(message);
            await stream.WriteAsync(data, 0, data.Length);
        }
    }
}