using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InstructionHighlights : MonoBehaviour
{
    Material[] materials;
    MeshRenderer[] renderers;
    private bool prev_highlighted;
    public  bool highlighted;
    public Material highlight_material;

    void SetMaterial() {
        if (highlighted) {
            for (int i = 0; i < renderers.Length; i++) {
                renderers[i].material = highlight_material;
            }
        }
        else {
            for (int i = 0; i < renderers.Length; i++) {
                renderers[i].material = materials[i];
            }
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        highlighted = false;
        renderers = this.gameObject.GetComponentsInChildren<MeshRenderer>();
        materials = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) materials[i] = renderers[i].material;
    }

    // Update is called once per frame
    void Update()
    {
        if (prev_highlighted != highlighted) SetMaterial();
        prev_highlighted = highlighted;
    }
}
