using UnityEngine;
using UnityEngine.Events;

namespace twitch
{
    public class TwitchCommandManager : MonoBehaviour
    {
        [field: SerializeField]
        private TwitchConnector twitchConnector;

        [field: SerializeField]
        private TwitchCommand[] commands;

        [field: SerializeField]
        private string helpCommand = "!help";

        void Start()
        {
            twitchConnector.processMessageListeners += ProcessMessage;
        }

        private void ProcessMessage(string sender, string message)
        {
            string possibleCommand;
            int index = message.IndexOf(' ');
            if (index < 0)
            {
                possibleCommand = message;
            }
            else
            {
                possibleCommand = message.Substring(0, index);
            }
            possibleCommand = possibleCommand.ToLower();

            // Special case for !help
            if (possibleCommand.Equals(helpCommand.ToLower()))
            {
                PrintCommands();
                return;
            }

            foreach (var command in commands)
            {
                if (possibleCommand.Equals(command.command.ToLower()))
                {
                    string[] arguments;
                    int substringStart = command.command.Length + 1;
                    if (message.Length > substringStart)
                    {
                        arguments = message.Substring(command.command.Length + 1).Split(' ');
                    }
                    else
                    {
                        arguments = new string[0];
                    }
                    command.unityEvent?.Invoke(sender, arguments);
                    break;
                }
            }
        }

        private void PrintCommands()
        {
            foreach (var command in commands)
            {
                twitchConnector.SendChatMessage(command.ToString());
            }
        }
    }

    [System.Serializable]
    public class TwitchCommand
    {
        [field: SerializeField]
        [field: Tooltip("Command to send in twitch, e.g., \"!foo\"")]
        public string command { get; private set; }

        [field: SerializeField]
        [field: Tooltip("Arguments (if any), e.g., [true|false]")]
        public string argumentDescription { get; private set; }

        [field: SerializeField]
        [field: Tooltip("Description of the command, e.g., !foo will perform a foo action if the argument is true, and a bar action otherwise.")]
        public string description { get; private set; }

        [field: SerializeField]
        public UnityEvent<string, string[]> unityEvent { get; private set; }

        public override string ToString()
        {
            string result = command;
            if (!string.IsNullOrEmpty(argumentDescription))
            {
                result += " " + argumentDescription;
            }
            if (!string.IsNullOrEmpty(description))
            {
                result += " - " + description;
            }
            return result;
        }
    }
}
