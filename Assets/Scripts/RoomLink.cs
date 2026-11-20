using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Carousel
{
    [Serializable]
    public class Seat
    {
        public int id;
        public string name;
        public bool bot, alive, connected;
    }

    [Serializable]
    public class Impact
    {
        public int seq, actor, target, seed;
        public bool spin, hit;
        public string effect;
    }

    [Serializable]
    public class TableState
    {
        public string phase;
        public int revision, round, turn, winner, host;
        public Seat[] players;
        public Impact shot;
        public Seat Find(int id)
        {
            return Array.Find(players, p => p.id == id);
        }
    }

    [Serializable]
    public class Envelope
    {
        public string type, message;
        public int me;
        public TableState state;
    }

    [Serializable]
    public class Command
    {
        public string type, name;
        public int protocol = 1, target, revision;
        public bool spin;
    }

    // One link owns one connection and, for practice/LAN hosting, one child process.
    public sealed class RoomLink : IDisposable
    {
        public readonly ConcurrentQueue<string> Lines = new ConcurrentQueue<string>();
        public readonly ConcurrentQueue<string> Failures = new ConcurrentQueue<string>();
        readonly object gate = new object ();
        TcpClient socket;
        StreamWriter writer;
        Process child;
        volatile bool closed;
        public string LocalAddresses { get; private set; } = "IP этого ПК";

        void FindLocalAddresses()
        {
            try
            {
                var addresses = NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback).SelectMany(n => n.GetIPProperties().UnicastAddresses).Select(a => a.Address).Where(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a)).Distinct().Take(3).Select(a => a.ToString()).ToArray();
                if (addresses.Length > 0)
                    LocalAddresses = string.Join(" / ", addresses);
            }
            catch (NetworkInformationException)
            {
            }
            catch (NotImplementedException)
            {
            }
        }

        public void Open(string host, int port, string executable, int bots, string hello)
        {
            Task.Run(async () =>
            {
                try
                {
                    if (executable != null)
                    {
                        if (bots == 0)
                            FindLocalAddresses();
                        var info = new ProcessStartInfo
                        {
                            FileName = executable,
                            Arguments = bots > 0 ? "-listen 127.0.0.1:0 -bots " + bots : "-listen :" + port,
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardInput = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        };
                        info.EnvironmentVariables["CAROUSEL_CHILD"] = "1";
                        Process proc;
                        lock (gate)
                        {
                            if (closed)
                                return;
                            child = proc = Process.Start(info);
                        }

                        string bound = await proc.StandardOutput.ReadLineAsync();
                        if (string.IsNullOrEmpty(bound))
                            throw new IOException("Сервер не запустился. " + await proc.StandardError.ReadToEndAsync());
                        port = int.Parse(bound.Substring(bound.LastIndexOf(':') + 1));
                        host = "127.0.0.1";
                    }

                    TcpClient tcp;
                    lock (gate)
                    {
                        if (closed)
                            return;
                        socket = tcp = new TcpClient
                        {
                            NoDelay = true,
                            SendTimeout = 1000
                        };
                    }

                    var connect = tcp.ConnectAsync(host, port);
                    if (await Task.WhenAny(connect, Task.Delay(7500)) != connect)
                        throw new TimeoutException("Истекло время подключения");
                    await connect;
                    var utf8 = new UTF8Encoding(false);
                    var stream = tcp.GetStream();
                    lock (gate)
                    {
                        if (closed)
                            return;
                        writer = new StreamWriter(stream, utf8)
                        {
                            AutoFlush = true,
                            NewLine = "\n"
                        };
                        writer.WriteLine(hello);
                    }

                    using (var reader = new StreamReader(stream, utf8))
                    {
                        var buffer = new char[1024];
                        var line = new StringBuilder();
                        int count;
                        while (!closed && (count = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            for (int i = 0; i < count; i++)
                            {
                                if (buffer[i] == '\n')
                                {
                                    if (Lines.Count >= 128)
                                        throw new IOException("Слишком много сообщений сервера");
                                    Lines.Enqueue(line.ToString());
                                    line.Clear();
                                }
                                else
                                {
                                    if (line.Length >= 8192)
                                        throw new IOException("Слишком длинное сообщение сервера");
                                    line.Append(buffer[i]);
                                }
                            }
                        }
                    }

                    if (!closed)
                        Failures.Enqueue("Соединение с комнатой закрыто");
                }
                catch (Exception e)
                {
                    if (!closed)
                        Failures.Enqueue(e.GetBaseException().Message);
                }
            });
        }

        public void Send(Command command)
        {
            try
            {
                lock (gate)
                {
                    if (!closed && writer != null)
                        writer.WriteLine(JsonUtility.ToJson(command));
                }
            }
            catch (Exception e)
            {
                Failures.Enqueue(e.Message);
            }
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (closed)
                    return;
                closed = true;
                socket?.Close();
                if (child != null)
                {
                    try
                    {
                        child.StandardInput.Close();
                        if (!child.HasExited)
                            child.Kill();
                    }
                    catch (InvalidOperationException)
                    {
                    }
                    finally
                    {
                        child.Dispose();
                    }
                }
            }
        }
    }
}
