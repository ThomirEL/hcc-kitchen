using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.AI;

/// <summary>
/// Displays a list of remaining targets on the right controller.
/// List toggles on/off with a button press.
/// 
/// Setup:
/// 1. Attach this script to an empty GameObject
/// 2. Create child hierarchy:
///    - ControllerListCanvas (Canvas, set to World Space)
///      - TargetList (empty container for text items)
/// 3. Assign references in Inspector
/// 4. Assign the right controller transform
/// </summary>
public class HandListUI : MonoBehaviour
{
    public void Start()
    {
        var inputDevices = new List<UnityEngine.XR.InputDevice>();
        UnityEngine.XR.InputDevices.GetDevices(inputDevices);
    }
    public void Update()
    {
        // Check if right controler primary button is pressed
        
    }

}
