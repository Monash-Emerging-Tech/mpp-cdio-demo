using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;

public class KeyPadInputHandler : MonoBehaviour
{
    public TMP_InputField inputText; // Reference to the TextMeshPro Input Field
    // Start is called before the first frame update
    void Start()
    {
        foreach (Transform child in gameObject.transform)
        {
            Button button = child.gameObject.GetComponent<Button>();
            // Cache the button's text value to avoid closure issues
            string buttonText = child.name;
            button.onClick.AddListener(() => AppendNumberToOutput(buttonText));
        }
    }

    void AppendNumberToOutput(string number)
    {
        inputText.text += number;
    }

    // Update is called once per frame
    void Update()
    {

    }
}
