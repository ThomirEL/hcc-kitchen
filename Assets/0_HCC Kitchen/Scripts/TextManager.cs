using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.Attachment;

public class TextManager : MonoBehaviour
{

    private int step = 0;
[SerializeField]
    GameObject textObject1;
[SerializeField]
    GameObject textObject2;
[SerializeField]
    GameObject inputField;

    [SerializeField]
    TMP_InputField inputFieldTMP;
[SerializeField]
    GameObject textObject3;
[SerializeField]
    GameObject textObject4;

[SerializeField]
    GameObject nextButton;
[SerializeField]
    GameObject startButton;

    public void incrementIntroduction()
    {
        step++;
        
        switch (step)
        {
            case 1:
                if (inputFieldTMP.text == "")
                {
                    Debug.Log("Input field is empty. Please enter a name.");
                    textObject1.GetComponent<TextMeshProUGUI>().text = "Please enter the ID to proceed.";
                    step--; // Stay on the same step
                    return;
                }
                textObject1.SetActive(false);
                textObject2.SetActive(true);
                inputField.SetActive(false);
                break;
            case 2:
                textObject2.SetActive(false);
                textObject3.SetActive(true);
                break;
            case 3:
                textObject3.SetActive(false);
                textObject4.SetActive(true);
                startButton.SetActive(true);
                break;
            default:
                Debug.Log("No more steps");
                break;
        }
    }
}
