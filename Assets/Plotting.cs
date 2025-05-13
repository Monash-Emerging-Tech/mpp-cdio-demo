using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// (isuru): Got plotting from Game Dev Guide https://www.youtube.com/watch?v=--LB7URk60A
public class Plotting : Graphic
{
    private float width;
    private float height;
    float thickness = 1.0f;
    float gridline_thickness = 0.5f;
    float axis_thickness = 2.0f;
    
    private CSVReader GodScript;

    protected override void Start() {
        base.Start();
        GodScript = GameObject.Find("God").GetComponent<CSVReader>();
    }

    protected override void OnPopulateMesh(VertexHelper vh) {
        if (GodScript.sim.steps_computed < 2) return;
        
        vh.Clear();

        float[] sim_data = GodScript.sim_data;
        int n_control = GodScript.n_control;
        int n_process = GodScript.n_process-1;
        width = rectTransform.rect.width;
        height = rectTransform.rect.height;
        int n_x_gridlines = 8;
        int n_y_gridlines = 10;
        Color32[] colours = new Color32[] {
            new Color(150f/255f,170f/255f,185f/255f),
            new Color(150f/255f,160f/255f,140f/255f),
            new Color(98f/255f,121f/255f,184f/255f),
            new Color(78f/255f,101f/255f,164f/255f),
            new Color(58f/255f,81f/255f,144f/255f),
            new Color(144/255f,169/255f,89f/255f),
            new Color(124/255f,149/255f,69f/255f)
        };
        Color32 axis_colour = new Color32(0,0,0,255);
        Color32 grid_colour = new Color32(0,0,0,127);
        int[] plot_params = new int[] {
            GodScript.FIC01_OP_manu_index,
            GodScript.PIC02_OP_manu_index,
            GodScript.FIT01_index,
            GodScript.FIT02_index,
            GodScript.FIT03_index,
            GodScript.PT01_index,
            GodScript.PT02_index,
        };
        float ymin = 0f;
        float ymax = 0.25f;
        int imin = 0;
        int imax = 0;
        for (int i = 0; i < plot_params.Length; i++) {
            int r = plot_params[i];
            for (int t = 0; t < GodScript.sim.steps_computed; t++) {
                float value = sim_data[r*GodScript.sim.steps_max+t];
                if (value < ymin) {
                    ymin = value;
                    imin = r;
                }
                if (value > ymax) {
                    ymax = value;
                    imax = r;
                }
            }
        }
        
        // draw x-axis
        DrawLine(vh, new Vector2(-axis_thickness/2, 0), new Vector2(width, 0), axis_thickness, axis_colour);
        // draw y-axis
        DrawLine(vh, new Vector2(0, -axis_thickness/2), new Vector2(0, height), axis_thickness, axis_colour);

        // draw horizontal grid lines
        float ydiff = ymax-ymin;
        float horizontal_gridline_separation;
        if      (ydiff < 1)     horizontal_gridline_separation = 0.1f;
        else if (ydiff < 10)    horizontal_gridline_separation = 1;
        else if (ydiff < 100)   horizontal_gridline_separation = 10;
        else if (ydiff < 1000)  horizontal_gridline_separation = 100;
        else if (ydiff < 10000) horizontal_gridline_separation = 1000;
        else                          horizontal_gridline_separation = 10000;
        int actual_n_x_gridlines = (int)Mathf.Floor(ydiff / horizontal_gridline_separation);
        for (int i = 0; i < actual_n_x_gridlines; i++) {
            DrawLine(
                vh,
                new Vector2(0,     (i+1) * horizontal_gridline_separation / ydiff * height),
                new Vector2(width, (i+1) * horizontal_gridline_separation / ydiff * height),
                gridline_thickness,
                grid_colour
            );
        }

        // draw vertical grid lines
        // @Incomplete(isuru 06/09/2024): Worth replacing with equivalent mathemical function? Maybe not
        float vertical_gridline_separation;
        if      (GodScript.sim.steps_computed < 10)    vertical_gridline_separation = 1;
        else if (GodScript.sim.steps_computed < 100)   vertical_gridline_separation = 10;
        else if (GodScript.sim.steps_computed < 1000)  vertical_gridline_separation = 100;
        else if (GodScript.sim.steps_computed < 10000) vertical_gridline_separation = 1000;
        else                                           vertical_gridline_separation = 10000;
        int actual_n_y_gridlines = (int)Mathf.Floor(GodScript.sim.steps_computed / vertical_gridline_separation);
        for (int i = 0; i < actual_n_y_gridlines; i++) {
            DrawLine(
                vh,
                new Vector2((i+1) * vertical_gridline_separation / GodScript.sim.steps_computed * width, 0),
                new Vector2((i+1) * vertical_gridline_separation / GodScript.sim.steps_computed * width, height),
                gridline_thickness,
                grid_colour
            );
        }
        
       
        // @Incomplete(isuru 06/09/2024): Fill gaps between line vertices
        int sf = (int)Mathf.Max(1f, Mathf.Ceil((float)GodScript.sim.steps_computed / 100));
        Vector2 v0 = new Vector2();
        Vector2 v1 = new Vector2();
        for (int i = 0; i < plot_params.Length; i++) {
            int r = plot_params[i];
            Color32 c = colours[i];
            for (int t = 0; t < GodScript.sim.steps_computed-sf; t += sf) {
                v0.x = width  * t/((float)GodScript.sim.steps_computed);
                v0.y = height * (sim_data[r*GodScript.sim.steps_max+t]-ymin)/(ymax-ymin);
                v1.x = width  * (t+sf)/((float)GodScript.sim.steps_computed);
                v1.y = height * (sim_data[r*GodScript.sim.steps_max+t+sf]-ymin)/(ymax-ymin);
                DrawLine(vh, v0, v1, thickness, c);
            }
        }
    }

    private void DrawLine(VertexHelper vh, Vector2 start, Vector2 end, float thickness, Color32 colour) {

        // create direction vectors
        Vector2 direction = (end - start).normalized;
        Vector2 perpendicular = new Vector2(-direction.y,direction.x) * thickness / 2f;

        // create corners of rectangle
        UIVertex v = UIVertex.simpleVert;
        v.color = colour;
        v.position = end - perpendicular;
        vh.AddVert(v);
        v.position = start - perpendicular;
        vh.AddVert(v);
        v.position = start + perpendicular;
        vh.AddVert(v);
        v.position = end + perpendicular;
        vh.AddVert(v);

        // create triangles clockwise
        int vidx = vh.currentVertCount;
        vh.AddTriangle(vidx - 4, vidx - 3, vidx - 2);
        vh.AddTriangle(vidx - 2, vidx - 1, vidx - 4);
    }
}
