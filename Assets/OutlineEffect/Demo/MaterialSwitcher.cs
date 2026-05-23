using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace cakeslice
{
    public class MaterialSwitcher : MonoBehaviour
    {
        public Material target;
        public int index;

        public void Update()
        {
            if(Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                Material[] materials = GetComponent<Renderer>().materials;
                materials[index] = target;
                GetComponent<Renderer>().materials = materials;
            }
        }
    }
}