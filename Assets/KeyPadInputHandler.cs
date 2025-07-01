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
            button.onClick.AddListener(
                () => AppendNumberToOutput(button.GetComponentInChildren<Text>().ToString()));
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
