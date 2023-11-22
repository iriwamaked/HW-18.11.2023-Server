using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Collections.Concurrent;

namespace Server
{
    internal class Program
    {

        private static ConcurrentDictionary<Socket, List<string>> clientsQuotes = new ConcurrentDictionary<Socket, List<string>>();
        private static readonly object lockObject = new object();

        static async Task Main(string[] args)
        {
            string ipAddress = "127.0.0.1";
            int port = 8080;
            IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Parse(ipAddress), port);

            using Socket listener = new Socket(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(ipEndPoint);
            listener.Listen(10);

            Console.WriteLine("Сервер запущен!");

            try
            {
                while (true)
                {
                    Socket client = await listener.AcceptAsync();
                    Console.WriteLine("Клиент присоединился");
                    
                    Task.Run(() => OneClientProcess(client));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Возникла ошибка: {ex.Message}");
            }
        }

        public static async Task OneClientProcess(Socket client)
        {
            try
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

                while (client.Connected)
                {
                    byte[] buffer = new byte[1024];
                    int bytesRead = await client.ReceiveAsync(buffer, SocketFlags.None);

                    if (bytesRead == 0)
                        break;

                    string message = Encoding.Unicode.GetString(buffer, 0, bytesRead);
                    Console.WriteLine($"Клиент: {message}");

                    if (message.ToLower() == "bye")
                        break;

                    string response = GetComputerResponse(client);
                    byte[] data = Encoding.Unicode.GetBytes(response);
                    await client.SendAsync(data, 0);

                    Console.WriteLine($"Ответ сервера: {response}");

                    if (response == "bye")
                    {
                        lock (lockObject)
                        {
                            clientsQuotes.TryRemove(client, out _);
                        }

                        client.Shutdown(SocketShutdown.Both);
                        client.Close();
                        Console.WriteLine("Соединение разорвано сервером из-за окончания цитат и направления сообщения \"bye\"");
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
                    client.Shutdown(SocketShutdown.Both);
                    client.Close();
                }
            }
        }

        public static string GetComputerResponse(Socket client)
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
    }
}