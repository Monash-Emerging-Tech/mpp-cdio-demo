using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class KeyPadInputHandler : MonoBehaviour
{
    public TMP_InputField inputText; // Reference to the TextMeshPro Input Field

    // Start is called before the first frame update
    void Start()
    {
        foreach (Button button in GetComponentsInChildren<Button>())
        {
            string buttonText = button.name; // Cache the button's text value
            if (buttonText == "Clear")
            {
                button.onClick.AddListener(() => Clear()); // Clear the input field
                continue; // Skip to the next button
            }
            button.onClick.AddListener(() => AppendNumberToOutput(buttonText));
        }
    }

    void AppendNumberToOutput(string number)
    {
        inputText.text += number;
    }

    void Clear()
    {
        inputText.text = ""; // Clear the input field
    }

    // Update is called once per frame
    void Update()
    {

    }
}
