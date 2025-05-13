using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaterSupply : MonoBehaviour
{
    bool prev_activated;
    public bool activated;
    public Color activated_colour;
    public Color deactivated_colour;
    MeshRenderer mesh_renderer;
    MeshRenderer spline_mesh_renderer;

    public void SetColour() {
        if (activated) {
            mesh_renderer.material.color        = activated_colour;
            spline_mesh_renderer.material.color = activated_colour;
        }
        else {
            mesh_renderer.material.color        = deactivated_colour;
            spline_mesh_renderer.material.color = deactivated_colour;
        }
    }

    public void ToggleActivation() {
        activated = !activated;
        prev_activated = activated;
        SetColour();
    }

    public bool IsActivated() {
        return activated;
    }

    void Start()
    {
        mesh_renderer = this.GetComponent<MeshRenderer>();
        spline_mesh_renderer = this.transform.Find("SplineExtrude").gameObject.GetComponent<MeshRenderer>();
        activated = true;
        SetColour();
    }

    void Update() {
        // (isuru): This is used for e.g. toggling activation through the Inspector
        if (prev_activated != activated) SetColour();
        prev_activated = activated;
    }
}
