using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using UnityEngine;

namespace twitch
{
    public class TwitchConnector : MonoBehaviour
    {
        public delegate void ProcessMessage(string sender, string message);
        public event ProcessMessage processMessageListeners;
        
        [System.Serializable]
        public enum ConnectionState
        {
            Disconnected,
            Connecting,
            Connected,
            Disconnecting,
        }

        [field: SerializeField]
        private bool initializeOnStart;
        [field: SerializeField]
        private string username;
        [field: SerializeField]
        private string accessCode;
        [field: SerializeField]
        private string chatroom;

        [field: SerializeField]
        private int numLinesPerInterval = 15;
        [field: SerializeField]
        private float interval = 30;

        public ConnectionState connectionState { get; private set; }

        private TcpClient twitchClient;
        private StreamReader reader;
        private StreamWriter writer;

        private int numLinesSent;
        private float intervalRemaining;
        private Queue<string> linesToSend;

        private IEnumerator readCoroutine;

        private static TwitchConnector instance = null;

        void Start()
        {
            if (instance != null)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            connectionState = ConnectionState.Disconnected;
            readCoroutine = null;

            numLinesSent = 0;
            intervalRemaining = interval;
            linesToSend = new Queue<string>();

            if (initializeOnStart)
            {
                Initialize();
            }
        }

        private void OnDestroy()
        {
            if (connectionState != ConnectionState.Disconnected)
            {
                connectionState = ConnectionState.Disconnecting;
            }
            if (twitchClient != null)
            {
                twitchClient.Close();
                twitchClient = null;
            }
        }

        void Update()
        {
            intervalRemaining -= Time.unscaledDeltaTime;
            if (intervalRemaining <= 0)
            {
                intervalRemaining = interval;
                numLinesSent = 0;
            }

            bool shouldFlush = false;
            while ((numLinesSent < numLinesPerInterval) && (linesToSend.Count > 0))
            {
                string lineToSend = linesToSend.Dequeue();
                writer.WriteLine(lineToSend);
                numLinesSent++;
                shouldFlush = true;
            }
            if (shouldFlush)
            {
                writer.Flush();
            }
        }

        public void Initialize(string username, string accessCode, string chatroom)
        {
            this.username = username;
            this.accessCode = accessCode;
            this.chatroom = chatroom;
            Initialize();
        }

        public void Initialize()
        {
            connectionState = ConnectionState.Connecting;

            username = username.ToLower();
            chatroom = chatroom.ToLower();

            if (twitchClient != null)
            {
                twitchClient.Close();
            }
            twitchClient = new TcpClient("irc.twitch.tv", 6667);
            reader = new StreamReader(twitchClient.GetStream());
            writer = new StreamWriter(twitchClient.GetStream());
            writer.WriteLine("PASS oauth:" + accessCode);
            writer.WriteLine("NICK " + username);
            writer.WriteLine("USER " + username + " 8 *:" + username);
            writer.WriteLine("JOIN #" + chatroom);
            writer.Flush();

            intervalRemaining = interval;
            numLinesSent = 4;
            connectionState = ConnectionState.Connected;

            if (readCoroutine == null)
            {
                readCoroutine = ReadStream();
                StartCoroutine(readCoroutine);
            }
        }

        public void SendChatMessage(string message)
        {
            if ((connectionState == ConnectionState.Connected) && (message.Length > 0))
            {
                SendLine("PRIVMSG #" + chatroom + " :" + message);
            }
        }

        private IEnumerator ReadStream()
        {
            while ((connectionState == ConnectionState.Connecting) || (connectionState == ConnectionState.Connected))
            {
                while (connectionState == ConnectionState.Connecting)
                {
                    yield return null;
                }
                if ((twitchClient.Available > 0) || (reader.Peek() >= 0))
                {
                    string line;
                    line = reader.ReadLine();
                    ProcessLine(line);
                }
                yield return null;
            }
        }

        private void ProcessLine(string line)
        {
            var (command, source, parameters) = ParseMessage(line);

            // Automatically reply to PING messages immediately
            if (command.Equals("PING"))
            {
                SendLine("PONG " + parameters, true);
                return;
            }
            else if (command.Equals("PRIVMSG"))
            {
                processMessageListeners?.Invoke(source, parameters);
            }
        }

        private void SendLine(string line, bool force = false)
        {
            if (force)
            {
                writer.WriteLine(line);
                writer.Flush();
                numLinesSent++;
            }
            else
            {
                linesToSend.Enqueue(line);
            }
        }

        // Returns (command, source, parameters)
        private (string, string, string) ParseMessage(string message)
        {
            // Converted to C# and modified from: https://dev.twitch.tv/docs/irc/example-parser

            string command;
            string source = null;
            string parameters = null;

            // The start index. Increments as we parse the IRC message
            int idx = 0;

            // The raw components of the IRC message
            //string rawTagsComponent = null;
            string rawCommandComponent;
            string rawSourceComponent = null;
            string rawParametersComponent = null;

            int endIdx;

            // If the message includes tags, get the tags component of the IRC message
            if (message[idx] == '@')
            {
                endIdx = message.IndexOf(' ');
                // We ignore the tags
                //rawTagsComponent = message.Substring(1, endIdx - 1);
                idx = endIdx + 1; // Should now point to the source colon (:).
            }

            // Get the source component (nick and host) of the IRC message.
            // The idx should point to the source part; otherwise, it's a PING command.
            if (message[idx] == ':')
            {
                idx += 1;
                endIdx = message.IndexOf(' ', idx);
                rawSourceComponent = message.Substring(idx, endIdx - idx);
                idx = endIdx + 1; // Should point to the command part of the message.
            }

            // Get the command component of the IRC message
            endIdx = message.IndexOf(':', idx); // Looking for the parameters parts of the message.
            if (endIdx == -1)                   // But not all messages include the parameters part.
            {
                endIdx = message.Length;
            }
            rawCommandComponent = message.Substring(idx, endIdx - idx).Trim();

            // Get the parameters component of the IRC message.
            if (endIdx != message.Length) // Check if the IRC message contains a parameters component.
            {
                idx = endIdx + 1;         // Should point to the parameters part of the message.
                rawParametersComponent = message.Substring(idx);
            }

            command = ParseCommand(rawCommandComponent);
            if (command != null)
            {
                source = ParseSource(rawSourceComponent);
                // We don't parse the raw parameters further
                parameters = rawParametersComponent?.Trim();
            }

            return (command, source, parameters);
        }

        private string ParseCommand(string rawCommandComponent)
        {
            if (string.IsNullOrEmpty(rawCommandComponent))
            {
                return null;
            }

            var commandParts = rawCommandComponent.Split(' ');
            return commandParts[0];
        }

        private string ParseSource(string rawSourceComponent)
        {
            if (string.IsNullOrEmpty(rawSourceComponent))
            {
                return null;
            }
            string[] sourceParts = rawSourceComponent.Split('!');
            if (sourceParts.Length == 2)
            {
                return sourceParts[0];
            }
            else
            {
                return null;
            }
        }
    }
}
