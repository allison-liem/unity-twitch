using UnityEngine;

namespace sample
{
    public class SampleCommandReceiver : MonoBehaviour
    {
        [field: SerializeField]
        private Light lightToChange;

        public void RedCommand(string source, string[] _)
        {
            Debug.Log(source + " turned the light red.");
            lightToChange.color = Color.red;
        }

        public void GreenCommand(string source, string[] _)
        {
            Debug.Log(source + " turned the light green.");
            lightToChange.color = Color.green;
        }

        public void BlueCommand(string source, string[] _)
        {
            Debug.Log(source + " turned the light blue.");
            lightToChange.color = Color.blue;
        }

        public void ColorCommand(string source, string[] arguments)
        {
            if (arguments.Length >= 3)
            {
                if (!float.TryParse(arguments[0], out var red))
                {
                    Debug.Log("Failed to parse red");
                    return;
                }
                if (!float.TryParse(arguments[1], out var green))
                {
                    Debug.Log("Failed to parse green");
                    return;
                }
                if (!float.TryParse(arguments[2], out var blue))
                {
                    Debug.Log("Failed to parse blue");
                    return;
                }
                Debug.Log(source + " turned the light to RGB (" + red + ", " + green + ", " + blue + ").");
                lightToChange.color = new Color(Mathf.Clamp(red, 0, 1), Mathf.Clamp(green, 0, 1), Mathf.Clamp(blue, 0, 1));
            }
        }
    }
}
