using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Linq;

namespace Server
{
    internal class ClientInfo
    {
        public TcpClient Client { get; set; }
        public string Login { get; set; }
        public List<string> ClientQuotes { get; set; }
    }

    internal class Program
    {
        private static List<ClientInfo> connectedClients = new List<ClientInfo>();
        private static Dictionary<string, string> clientsIdentity;
        //private static ConcurrentDictionary<TcpClient, List<string>> clientsQuotes = new ConcurrentDictionary<TcpClient, List<string>>();
        private static readonly object lockObject = new object();
        const int port = 8080;
        static IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), port);
        static TcpListener listener;
        private static int currentClientCount = 0;
        private static readonly object countLockObject = new object();
        static int maxClientCount = 3;
        static StreamWriter logWriter;
        static async Task Main(string[] args)
        {
            listener = new TcpListener(ipEndPoint);
            listener.Start();
            InitializeClients();
            Console.WriteLine("Сервер запущен!");
            logWriter = new StreamWriter("log.txt", true);
            try
            {
                while (true)
                {
                    TcpClient client = await listener.AcceptTcpClientAsync();

                    if (await TakeClientCredentials(client))
                    {
                        if (CanAcceptClient())
                        {
                            Log($"Присоединился клиент {connectedClients.FirstOrDefault(info => info.Client == client).Login}: {client.Client.RemoteEndPoint}, {DateTime.Now}");
                            await WriteMessageAsync(client, "Hello");

                            IncrementClientCount();

                            _ = OneClientProcess(client);
                        }
                        else
                        {
                            connectedClients.RemoveAt(connectedClients.Count - 1);
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

        private static bool ValidateClient(string login, string password)
        {
            if (clientsIdentity.TryGetValue(login, out var currentPassword))
            {
                return password == currentPassword;
            }
            return false;
        }

        private static async Task<bool> TakeClientCredentials(TcpClient client)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                byte[] buffer = new byte[1024];
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                string clientCredentials = Encoding.Unicode.GetString(buffer, 0, bytesRead);

                if (clientCredentials != null)
                {
                    string[] credentialsArray = clientCredentials.Split(',');
                    string login = credentialsArray[0];
                    string password = credentialsArray[1];
                    if (ValidateClient(login, password))
                    {
                        ClientInfo clientInfo = new ClientInfo() { Client = client, Login = login };
                        connectedClients.Add(clientInfo);
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
                Log($"Ошибка при обработке учетных данных клиента: {ex.Message}");
                return false;
            }

        }

        private static bool CanAcceptClient()
        {
            lock (countLockObject)
            {
                return currentClientCount < maxClientCount;
            }
        }

        private static void IncrementClientCount()
        {
            lock (countLockObject)
            {
                currentClientCount++;
            }
        }

        private static void DecrementClientCount()
        {
            lock (countLockObject)
            {
                currentClientCount--;
            }
        }

        public static void InitializeQuotes(ClientInfo clientInfo)
        {
            lock (lockObject)
            {
                clientInfo.ClientQuotes = new List<string>
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
                    };
            }
        }

        public static async Task OneClientProcess(TcpClient client)
        {
            NetworkStream stream = null;
            try
            {
                stream = client.GetStream();

                ClientInfo clientInfo = connectedClients.FirstOrDefault(info => info.Client == client);
                InitializeQuotes(clientInfo);

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
                    Console.WriteLine($"Клиент {clientInfo.Login}: {message}");

                    if (message.ToLower() == "bye")
                    {
                        Log($"Клиент {connectedClients.FirstOrDefault(info => info.Client == client).Login}: {client.Client.RemoteEndPoint} отключился в: {DateTime.Now}");
                        DecrementClientCount();
                        break;
                    }

                    string response = GetComputerResponse(clientInfo);
                    data = Encoding.Unicode.GetBytes(response);
                    await stream.WriteAsync(data, 0, data.Length);

                    Log($"Цитата для клиента {clientInfo.Login}: {response}");

                    if (response == "bye")
                    {
                        lock (lockObject)
                        {
                            //clientsQuotes.TryRemove(client, out _);
                            connectedClients.RemoveAll(info => info.Client == client);
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
                //lock (lockObject)
                //{
                //    clientsQuotes.TryRemove(client, out _);
                //}

                if (client != null && client.Connected)
                {
                    client.Close();
                }
            }
        }

        public static string GetComputerResponse(ClientInfo clientInfo)
        {
            lock (lockObject)
            {
                if (clientInfo.ClientQuotes != null && clientInfo.ClientQuotes.Any())
                {
                    Random random= new Random();
                    int index = random.Next(clientInfo.ClientQuotes.Count);
                    string quote = clientInfo.ClientQuotes[index];
                    clientInfo.ClientQuotes.RemoveAt(index);

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
                logWriter.WriteLine(message);
                logWriter.Flush();
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