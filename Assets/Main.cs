// @IncompleteVisuals0(isuru 03/09/2024): Consolidate some of the lines (some stray sections of lines in Additions1)
// @IncompleteVisuals0(isuru 03/09/2024): Make prop valve visual part of the HUD rather than in-world
// @IncompleteVisuals0(isuru 04/09/2024): Sensors
// @IncompleteVisuals0(isuru 06/09/2024): Display active alarms
// #define TEST_PLOT

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.Networking;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Text;
using System.IO;

using cakeslice;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

public class CSVReader : MonoBehaviour {
    public TextAsset ValveMetadata;
    public TextAsset PathwaysTxtFile;
    public TextAsset PumpSourcesTxtFile;
    public TextAsset ProcedureFSM;
    public GameObject[] prefabs;

    private Dictionary<string,int> prefab_dict = new Dictionary<string,int>();

    List<string[]> import_pathways(TextAsset txt_file) {
        List<string[]> pathways = new List<string[]>();
        string[] lines = txt_file.text.Split(new string[] {"\n"}, StringSplitOptions.None);
        foreach (string p in lines) {
            string[] nodes = p.Split(new String[] {" "}, StringSplitOptions.None);
            pathways.Add(nodes);
        }
        return pathways;
    }
    private List<string[]> pathways;

    [System.Serializable]
    public class MSPP_Part {
        public string name;
        public string category;
        public string type;
        public float x;
        public float y;
        public float z;
        public float rx;
        public float ry;
        public float rz;
        public float h;
    }
    public MSPP_Part[] part_definitions;

    private GameObject[] part_objects;

    public GameObject[] switches;

    // Pumps
    public Dictionary<string,bool> pumps = new Dictionary<string,bool>();

    [System.Serializable]
    public class MPP_Vessel {
        private float _volume;
        public float radius;
        public float max_height;
        public float max_volume;

        public float volume {
            get => _volume;
            set => _volume = value;
        }
    };
    public Dictionary<string,MPP_Vessel> vessels = new Dictionary<string, MPP_Vessel>();
    
    // Alarms
    public Dictionary<string,bool> alarms = new Dictionary<string, bool>();

    // Configuration valves
    private Dictionary<string,bool> configuration_valves = new Dictionary<string,bool>();
    // Proportional valves
    private Dictionary<string,float> proportional_valves = new Dictionary<string,float>();
    // Proportional control valves
    [System.Serializable]
    public struct MPP_PCV_State {
        public bool automatic_manual;
        public float operating_point_manual;
        public float setpoint;
        public string process_variable_source;
    }
    public Dictionary<string,MPP_PCV_State> proportional_control_valves = new Dictionary<string,MPP_PCV_State>();

    public float demo_speed;
    public float seconds_elapsed;

    public enum MPP_Event_Type: int {
        none=0,
        configuration_valve_interaction, //
        proportional_valve_interaction, //
        proportional_control_valve_interaction, //
        pump_interaction,
        // switch_interaction, // @Incomplete1(isuru 27/05/2024): Initially we are not going to have switch interactions
        // alarm_activation,   // @Incomplete1(isuru 27/05/2024): Initially we are not going to have alarms
        count,
    }

    [System.Serializable]
    // @Incomplete1(isuru 27/05/2024): This currently doesn't account for events which have knock-on effects (e.g. alarm activations)
    public struct MPP_Event {
        public float          seconds_elapsed;
        public int            t_sim_step;
        public string         fsm_state; // @IncompleteFSM: Fix up elsewhere
        public MPP_Event_Type type;
        public string         name;
        public float          before_float;
        public float          after_float;
        public bool           before_bool;
        public bool           after_bool;
    };
    public List<MPP_Event> event_queue;
    // (isuru): The event queue index points at the event to be undone when the user next hits undo
    // We need this to allow redo, as we only clear undone events from the queue when the user overwrites
    // them with another interaction or the time at which the undone event was to occur passes
    public int event_queue_index;

    [System.Serializable]
    public struct MPP_Simulation_State {
        public float seconds_elapsed;
        public float step_length;
        public int   steps_computed;
        public int   steps_elapsed;
        public int   steps_max;
    };
    public MPP_Simulation_State sim;

    public float[] sim_data;
    public int n_timestep = 1;
    public int n_control = 2;
    public int n_process = 5;
    public int FIC01_OP_manu_index = 0;
    public int PIC02_OP_manu_index = 1;
    public int FIT01_index         = 2;
    public int FIT02_index         = 3;
    public int FIT03_index         = 4;
    public int PT01_index          = 5;
    public int PT02_index          = 6;
    
    // graph testing data
    public int test_FIC01_OP_manu_index = 4;
    public int test_PIC02_OP_manu_index = 11;
    public int test_FIT01_index         = 5;
    public int test_FIT02_index         = 6;
    public int test_FIT03_index         = 7;
    public int test_PT01_index          = 12;
    public int test_PT02_index          = 13;

    public float speed = 100.0f;
    public float lookspeed = 540.0f;
    private GameObject selected_part = null;
    private GameObject selected_prop_valve = null;
    private string selected_prop_valve_name = null;
    private float prop_valve_edit_y = 0.0f;
    private float prop_valve_edit_val = 0.0f;
    private GameObject prop_valve_visual;
    private float pvv_width = 12.0f;
    private float pvv_height = 3.0f;

    public float camera_height;
    public bool  camera_free_not_constrained;
    private EventSystem event_system;

    // XR Action controllers
    public ActionBasedController leftController;
    public ActionBasedController rightController;

    System.Random rnd = new System.Random();

    string[][] linked_parts = {
        // VP01-VP02
        new string[3]{ "VP01", "VP02", "+" },
        new string[3]{ "VP02", "VP01", "+" },
    };
    string[] uneditable_parts = {
        // // External
        // "P03",
        // // UF
        // "VP01",
        // "VP02",
        // "VP04",
        // "VP05",
        // "VP06",
        // "VP07",
        // // RO
        // "P02",
        // "V08",
        // "V09",
        // "V10",
        // "V11",
        // "V14",
        // "V23",
        // // HEX
        // "V21",
        // "V22",
    };
    Color uneditable_colour = new Color(0.6f,0.6f,0.6f,0.3f);
    public Material uneditable_material;
    
    string[] threeway_pneumatic_valves = {
        "VP01",
        "VP02",
    };
    string[] pneumatic_valves = {
        "VP03",
        "VP04",
        "VP05",
        "VP06",
        "VP07",
        "VP08",
        "VP09",
        "FCV01",
        "PCV02",
    };
    string[] electric_valves = {
        "EV01"
    };

    void turn_configuration_valve(GameObject valve) {
        bool is_pneumatic2 = false;
        foreach (string pneumatic_valve in pneumatic_valves) {
            if (valve.name == pneumatic_valve) {
                is_pneumatic2 = true;
                break;
            }
        }
        bool is_pneumatic3 = false;
        foreach (string pneumatic_valve in threeway_pneumatic_valves) {
            if (valve.name == pneumatic_valve) {
                is_pneumatic3 = true;
                break;
            }
        }

        Transform element_obj;
        try {
            element_obj = valve.transform.Find("Element");
            if (configuration_valves[valve.name]) {
                if      (is_pneumatic2) element_obj.transform.localEulerAngles = new Vector3(0, -90,   0);
                else if (is_pneumatic3) element_obj.transform.localEulerAngles = new Vector3(0,  90,   0);
                else                    element_obj.transform.localEulerAngles = new Vector3(0,   0, -90);
            }
            else {
                element_obj.transform.localEulerAngles = new Vector3(0, 0, 0);
            }
        }
        catch {}
    }

    void set_pump(string pump_name, bool value) {
        pumps[pump_name] = value;
        MeshRenderer mesh_renderer = GameObject.Find(pump_name).GetComponentInChildren<MeshRenderer>();
        if (value) mesh_renderer.material = pump_active_material;
        else       mesh_renderer.material = pump_inactive_material;
    }

    GameObject FIT01_sensor;
    GameObject FIT02_sensor;
    GameObject FIT03_sensor;
    GameObject FIT04_sensor;
    GameObject FIT05_sensor;
    GameObject PT01_sensor;
    GameObject PT02_sensor;
    GameObject PT03_sensor;
    GameObject PT04_sensor;
    TMPro.TMP_Text FIT01_value_field;
    TMPro.TMP_Text FIT02_value_field;
    TMPro.TMP_Text FIT03_value_field;
    TMPro.TMP_Text FIT04_value_field;
    TMPro.TMP_Text FIT05_value_field;
    TMPro.TMP_Text PT01_value_field;
    TMPro.TMP_Text PT02_value_field;
    TMPro.TMP_Text PT03_value_field;
    TMPro.TMP_Text PT04_value_field;

    public List<GameObject> highlighted_lines;
    public bool highlight_flow_lines;
    public GameObject lines_group_gobj;

    void OnOperatingPointChange(GameObject gobj, int idx) {
        // @Incomplete1(isuru 26/05/2024): Rename SPs to OP in the prefab + code
        Debug.Log(String.Format("{0} setpoint change triggered", gobj.name));
        TMPro.TMP_InputField value_edited = gobj.GetComponentInChildren<TMPro.TMP_InputField>();
        TMPro.TMP_Text value_display = null;
        foreach (TMPro.TMP_Text text in gobj.GetComponentsInChildren<TMPro.TMP_Text>()) {
            if (text.name == "Value_SP") {
                value_display = text;
                break;
            } 
        }
        if (value_edited != null && value_display != null) {
            float value = float.Parse(value_edited.text);
            value = Mathf.Clamp(value, 0, 100) / 100f;
            value_display.text = value.ToString("P");
            MPP_PCV_State valve_state = proportional_control_valves[gobj.name];

            // Adding interaction to event queue
            MPP_Event ev = new MPP_Event();
            ev.type       = MPP_Event_Type.proportional_control_valve_interaction;
            ev.seconds_elapsed = seconds_elapsed;
            ev.t_sim_step = sim.steps_computed;
            ev.fsm_state = fsm_state;
            ev.name = gobj.name;
            ev.before_float = valve_state.operating_point_manual;
            ev.after_float = value;
            QueueEvent(ev, ref event_queue, ref event_queue_index);

            valve_state.operating_point_manual = value;
            proportional_control_valves[gobj.name] = valve_state;
            sim_data[idx*(int)sim.steps_max+sim.steps_computed-1] = value;
        }
        else {
            if (value_edited == null) Debug.Log("ERROR: Value_SP_Edit not found");
            if (value_display == null) Debug.Log("ERROR: Value_SP not found");
        }
    }

    GameObject FindTextField(GameObject gobj, string field_name) {
        foreach (TMPro.TMP_Text field in gobj.GetComponentsInChildren<TMPro.TMP_Text>()) {
            if (field.name == field_name) return field.gameObject;
        }
        // @Incomplete0(isuru 04/09/2024): Handle this better (handling case where the Field can't be found)
        throw new Exception();
        return new GameObject();
    }

    class MPP_Action {
        string part;
        float  new_state;
    }

    class MPP_Expression {
        public string lnode;
        public char op;
        public string rnode;

        public MPP_Expression(string text) {
            // @Assumption(isuru 03/09/2024): Assuming that exactly one (1) operator will be in the expression
            // @IncompleteFSM(isuru 03/09/2024): Will generalised expression parsing be necessary !?!?!?!
            int equal_index   = text.IndexOf('=', 0);
            int noteq_index   = text.IndexOf('!', 0);
            int greater_index = text.IndexOf('>', 0);
            int less_index    = text.IndexOf('<', 0);
            if (equal_index > -1) {
                this.op = '=';
                this.lnode = text.Substring(0, equal_index).Trim();
                this.rnode = text.Substring(equal_index+1, text.Length-equal_index-1).Trim();
            }
            else if (noteq_index > -1) {
                this.op = '!';
                this.lnode = text.Substring(0, noteq_index).Trim();
                this.rnode = text.Substring(noteq_index+1, text.Length-noteq_index-1).Trim();
            }
            else if (greater_index > -1) {
                this.op = '>';
                this.lnode = text.Substring(0, greater_index).Trim();
                this.rnode = text.Substring(greater_index+1, text.Length-greater_index-1).Trim();
            }
            else if (less_index > -1) {
                this.op = '<';
                this.lnode = text.Substring(0, less_index).Trim();
                this.rnode = text.Substring(less_index+1, text.Length-less_index-1).Trim();
            }
            else {
                Debug.Log("Error: Couldn't parse expression " + text);
            }
        }

        override public string ToString() {
            return this.lnode + " " + this.op + " " + this.rnode;
        }
    }

    public class MPP_Instruction {
        string              raw_text;
        public string       text;
        public List<string> referenced_parts;
        public List<string> referenced_parts_red;

        public MPP_Instruction(string raw_text, string text) {
            this.raw_text = raw_text;
            this.text     = text;
            this.referenced_parts = new List<string>(){};
            this.referenced_parts_red = new List<string>(){};
        }
    }

    class MPP_ProcedureUpdate {
        string         current_state;
        MPP_Expression transition;
        string         next_state;
    }
    
    [System.Serializable]
    public class MPP_Procedure {
        private Dictionary<string, MPP_Instruction>                    instructions = new Dictionary<string, MPP_Instruction>();
        private Dictionary<string, Dictionary<MPP_Expression, string>> transitions  = new Dictionary<string, Dictionary<MPP_Expression, string>>();
        private string initial_state;
        private string current_state;
        private string last_state_with_instruction;
        private List<string> history;

        private string[] illegal_keywords = new string[] {
            "digraph",
            "node",
        };

        // @IncompleteFSM0(isuru 04/09/2024): Error checking (that referenced parts exist)
        // @IncompleteFSM(isuru 02/09/2024): Extremely hacky parser rn, do something better (see @Assumptions)
        public MPP_Procedure(TextAsset file) {
            this.current_state = "";
            this.history       = new List<string>();
            // Import procedure
            bool first_state_found = false;
            string[] lines = file.text.Split('\n');
            for (int line_idx = 0; line_idx < lines.Length; line_idx++) {
                // Ignoring // comments
                string line = lines[line_idx].Split("//")[0];
                // Stripping whitespace + tokenising
                string[] tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                for (int tidx = 0; tidx < tokens.Length; tidx++) tokens[tidx] = tokens[tidx].Trim();
                if (tokens.Length > 1) {

                    // If the line starts with an illegal keyword, skip it
                    if (Array.IndexOf(this.illegal_keywords, tokens[0]) > -1) {}
                    else {
                        // Handling transitions and instructions
                        int arrow_idx = Array.IndexOf(tokens, "->");
                        if (arrow_idx > -1) {
                            // @Assumption(isuru 02/09/2024):
                            // - the two nodes are tokens 0 and 2
                            // - the arrow is token 1
                            // - [label= is token 3
                            // - ] is token -1

                            // Transition
                            // Extract states
                            string statea = tokens[0];
                            string stateb = tokens[2];
                            // Extract transition
                            string transition = "";
                            for (int tidx = 4; tidx < tokens.Length-1; tidx++) transition += tokens[tidx] + " ";
                            transition = transition.Split('\"')[1];
                            MPP_Expression expression = new MPP_Expression(transition);
                            // Storing the state-transition-state triple for use
                            // @IncompleteFSM(isuru 02/09/2024): Error checking (state-transition already associated with a target state)
                            if (!this.transitions.ContainsKey(statea)) this.transitions[statea] = new Dictionary<MPP_Expression, string>(){};
                            this.transitions[statea][expression] = stateb;
                        }
                        else {
                            // Instruction
                            // Extracting state and instruction text
                            // @Assumption(isuru 02/09/2024):
                            // - the two nodes are tokens 0
                            // - [label= is token 1
                            // - ] is token -1
                            string state = tokens[0];
                            string raw_text = "";
                            for (int tidx = 2; tidx < tokens.Length-1; tidx++) raw_text += tokens[tidx] + " ";
                            raw_text = raw_text.Split('\"')[1];
                            // @IncompleteFSM(isuru 02/09/2024): Error checking (state already associated with an instruction)
                            // Finding indices in instruction text where parts are referenced
                            List<int> i0 = new List<int>();
                            List<int> i1 = new List<int>();
                            i1.Add(-1);
                            for (int i = 0; i < raw_text.Length; i++) {
                                if (raw_text[i] == '{') i0.Add(i);
                                if (raw_text[i] == '}') i1.Add(i);
                            }
                            i0.Add(raw_text.Length);
                            // Constructing formatted instruction text
                            string formatted_text = "<color=#000000FF>";
                            for (int idx = 0; idx < i0.Count-1; idx++) {
                                for (int i = i1[idx]+1; i < i0[idx]; i++) {
                                    formatted_text += raw_text[i];
                                }
                                if (raw_text[i0[idx]+1] == '-') {
                                    formatted_text += "<color=#C27070FF>";
                                    for (int i = i0[idx]+2; i < i1[idx+1]; i++) formatted_text += raw_text[i];
                                }
                                else {
                                    formatted_text += "<color=#87ACB5FF>";
                                    for (int i = i0[idx]+1; i < i1[idx+1]; i++) formatted_text += raw_text[i];
                                }
                                formatted_text += "</color>";
                            }
                            for (int i = i1[i1.Count-1]+1; i < raw_text.Length; i++) {
                                formatted_text += raw_text[i];
                            }
                            // Creating the instruction and storing the names of parts referenced in it
                            MPP_Instruction instruction = new MPP_Instruction(raw_text, formatted_text);
                            for (int idx = 0; idx < i0.Count-1; idx++) {
                                string part_name = raw_text.Substring(i0[idx]+1, i1[idx+1]-i0[idx]-1);
                                if (part_name[0] == '-') {
                                    instruction.referenced_parts_red.Add(part_name.Substring(1));
                                }
                                else {
                                    instruction.referenced_parts.Add(part_name);
                                }
                            }
                            // Storing the state-instruction pair for use
                            this.instructions[state] = instruction;
                            if (!first_state_found) {
                                this.initial_state = state;
                                first_state_found = true;
                            }
                        }
                    }
                }
            }
            if (!first_state_found) Debug.Log("Error: Procedure has no states defined");
        }

        public bool update_state(
            Dictionary<string, bool>         configuration_valves,
            Dictionary<string, float>        proportional_valves,
            Dictionary<string,MPP_PCV_State> proportional_control_valves,
            Dictionary<string, bool>         pumps,
            Dictionary<string, MPP_Vessel>   vessels,
            Dictionary<string, bool>         alarms
        ) {
            string new_state = this.current_state;
            if (new_state == "") {
                new_state = this.initial_state;
                this.history.Add(new_state);
                if (this.instructions.ContainsKey(new_state)) this.last_state_with_instruction = new_state;
            }
            
            int iterations = 0;
            bool changed = true;
            List<MPP_ProcedureUpdate> updates = new List<MPP_ProcedureUpdate>();
            while (changed) {
                changed = false;
                if (this.transitions.ContainsKey(new_state)) {
                    foreach (var item in this.transitions[new_state]) {
                        MPP_Expression expression = item.Key;
                        bool evaluate_bool_expression(MPP_Expression expression, bool lnode_value){
                            bool expression_satisfied = false;
                            bool rnode_value = expression.rnode == "1";
                            if (expression.op == '=') expression_satisfied = (lnode_value == rnode_value);
                            if (expression.op == '!') expression_satisfied = (lnode_value != rnode_value);
                            return expression_satisfied;
                        }

                        bool evaluate_float_expression(MPP_Expression expression, float lnode_value){
                            float tolerance = 0.001f;
                            bool expression_satisfied = false;
                            float rnode_value = float.Parse(expression.rnode);
                            if      (expression.op == '=') expression_satisfied = (Math.Abs(lnode_value-rnode_value) < tolerance);
                            else if (expression.op == '!') expression_satisfied = (Math.Abs(lnode_value-rnode_value) > tolerance);
                            else if (expression.op == '>') expression_satisfied = (lnode_value >  rnode_value);
                            else if (expression.op == '<') expression_satisfied = (lnode_value <  rnode_value);
                            return expression_satisfied;
                        }

                        bool expression_satisfied = false;
                        if (configuration_valves.ContainsKey(expression.lnode))             expression_satisfied = evaluate_bool_expression(expression, configuration_valves[expression.lnode]);
                        else if (proportional_valves.ContainsKey(expression.lnode))         expression_satisfied = evaluate_float_expression(expression, proportional_valves[expression.lnode]);
                        else if (proportional_control_valves.ContainsKey(expression.lnode)) expression_satisfied = evaluate_float_expression(expression, proportional_control_valves[expression.lnode].operating_point_manual);
                        else if (pumps.ContainsKey(expression.lnode))                       expression_satisfied = evaluate_bool_expression(expression, pumps[expression.lnode]);
                        else if (vessels.ContainsKey(expression.lnode))                     expression_satisfied = evaluate_float_expression(expression, vessels[expression.lnode].volume);
                        else if (alarms.ContainsKey(expression.lnode))                      expression_satisfied = evaluate_bool_expression(expression, alarms[expression.lnode]);
                        else {
                            Debug.Log("Error: Why are we here" + expression.ToString());
                        }
                        if (expression_satisfied) {
                            new_state = item.Value;
                            changed = true;
                            this.history.Add(new_state);
                            if (this.instructions.ContainsKey(new_state)) this.last_state_with_instruction = new_state;
                        }
                    }
                }
                else {
                    Debug.Log("Current state " + new_state + " has no outgoing transitions");
                }

                iterations += 1;
                if (iterations > 100) {
                    Debug.Log("Likely loop detected");
                    string printstatement = "";
                    foreach (string state in this.history) printstatement += state + ", ";
                    Debug.Log(printstatement);
                    throw new Exception();
                }
            }

            bool state_changed = new_state != this.current_state;
            this.current_state = new_state;
            return state_changed;
        }

        public MPP_Instruction get_instruction() {
            return this.instructions[this.last_state_with_instruction];
        }
    }

    string fsm_state = ""; // @IncompleteFSM(isuru): Placeholder currently referenced in undo/redo stuff - fix up
    MPP_Procedure procedure;
    GameObject instruction_text;
    List<GameObject> highlighted_parts;
    List<GameObject> highlighted_parts_red;
    public bool highlight_instruction_parts;
    public Material highlight_material;

    public Material default_line_material;
    public Material line_highlight_material;

    public Material pump_active_material;
    public Material pump_inactive_material;

    void Start()
    {
        procedure = new MPP_Procedure(ProcedureFSM);
        instruction_text = GameObject.Find("InstructionText");
        highlighted_parts = new List<GameObject>();
        highlighted_parts_red = new List<GameObject>();

        event_queue = new List<MPP_Event>();
        event_queue_index = -1;

        for (int i = 0; i < lines_group_gobj.transform.childCount; i++) {
            GameObject line = lines_group_gobj.transform.GetChild(i).gameObject;
            line.AddComponent<InstructionHighlights>();
            line.GetComponent<InstructionHighlights>().highlight_material = highlight_material;
        }

        // Starting the simulation at the point where there is already enough data to make predictions
        sim.step_length = 1.0f;
        sim.seconds_elapsed = n_timestep*sim.step_length;
        sim.steps_elapsed = Math.Min((int)(sim.seconds_elapsed / sim.step_length), sim.steps_max-1);
        sim.steps_computed = sim.steps_elapsed;

        highlight_instruction_parts = true;
        highlighted_lines = new List<GameObject>();
        highlight_flow_lines = false;

        prop_valve_visual = GameObject.Find("ProportionalValveValueVisual");
        prop_valve_visual.SetActive(false);
        MeshFilter pvv_mesh_filter = prop_valve_visual.GetComponent<MeshFilter>();
        Mesh pvv_mesh = new Mesh();
        pvv_mesh_filter.mesh = pvv_mesh;
        pvv_mesh.subMeshCount = 2;
        pvv_mesh.vertices = new Vector3[6]{
            new Vector3(0,0,0),
            new Vector3(-pvv_width,pvv_height,0),
            new Vector3(-pvv_width,0,0),
            new Vector3(0,0,0.01f),
            new Vector3(0,0,0.01f),
            new Vector3(0,0,0.01f),
        };
        int[] tri_list = new int[] {0,1,2,3,4,5};
        pvv_mesh.SetTriangles(tri_list, 0, 3, 0);
        pvv_mesh.SetTriangles(tri_list, 3, 3, 1);

        // Allow outside event system
        event_system = GameObject.Find("EventSystem").GetComponent<EventSystem>();


#if TEST_PLOT
        // import graph testing data
        string path = "Assets\\test_data.csv";
        List<string[]> rows = new List<string[]>();
        var reader = new StreamReader(path);
        int row_count = 0;
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            var values = line.Split(',');
            if (row_count != 0) rows.Add(values);
            row_count++;
        }

        sim_data = new float[(n_control + n_process) * sim.steps_max];
        for (int i = 0; i < sim.steps_max; i++)
        {
            sim_data[0*sim.steps_max + i] = float.Parse(rows[i][test_FIC01_OP_manu_index]);
            sim_data[1*sim.steps_max + i] = float.Parse(rows[i][test_PIC02_OP_manu_index]);
            sim_data[2*sim.steps_max + i] = float.Parse(rows[i][test_FIT01_index]);
            sim_data[3*sim.steps_max + i] = float.Parse(rows[i][test_FIT02_index]);
            sim_data[4*sim.steps_max + i] = float.Parse(rows[i][test_FIT03_index]);
            sim_data[5*sim.steps_max + i] = float.Parse(rows[i][test_PT01_index]);
            sim_data[6*sim.steps_max + i] = float.Parse(rows[i][test_PT02_index]);
        }
#else
        sim_data = new float[1*(n_control+n_process)*sim.steps_max];
        for (int t = 0; t < sim.steps_max; t++) {
            sim_data[FIC01_OP_manu_index*sim.steps_max+t] = 0f;
            sim_data[PIC02_OP_manu_index*sim.steps_max+t] = 0f;
        }
        for (int i = n_control; i < n_control+n_process; i++) {
            for (int t = 0; t < n_timestep; t++) {
                sim_data[i*sim.steps_max+t] = 0f;
            }
        }
#endif
        
        for (int i = 0; i < prefabs.Length; i++) {
            this.prefab_dict[prefabs[i].name] = i;
        }

        { // Import parts
            string[] data = ValveMetadata.text.Split(new string[] {",", "\n"}, StringSplitOptions.None);
            int num_cols = 10;
            int num_rows = data.Length / num_cols;
            int num_hrows = 3;
            part_definitions = new MSPP_Part[num_rows-num_hrows];
            part_objects = new GameObject[num_rows-num_hrows];

            GameObject pos = GameObject.Find("Position");

            for (int i = num_hrows; i < num_rows; i++) {
                MSPP_Part part = new MSPP_Part();
                part.name = data[num_cols*i + 0];
                part.category = data[num_cols*i + 1];
                part.type = data[num_cols*i + 2];
                part.x  = float.Parse(data[num_cols*i + 3]);
                part.y  = float.Parse(data[num_cols*i + 4]);
                part.z  = float.Parse(data[num_cols*i + 5]);
                part.rx = float.Parse(data[num_cols*i + 6]);
                part.ry = float.Parse(data[num_cols*i + 7]);
                part.rz = float.Parse(data[num_cols*i + 8]);
                part_definitions[i-num_hrows] = part;
                GameObject prefab = prefabs[0];
                try {
                    prefab = prefabs[prefab_dict[part.type]];
                }
                catch {
                    Debug.Log(String.Format("ERROR: Prefab {0} missing", part.type));
                }
                GameObject gobj = (GameObject)Instantiate(
                    prefab,
                    new Vector3(-part.x/1000, part.z/1000, -part.y/1000), // VR: position units scaled down from /10
                    Quaternion.identity
                );
                gobj.transform.eulerAngles = new Vector3(part.rx, part.rz, part.ry);

                // VR Compatibility: Add scale and position offset
                gobj.transform.parent = pos.transform;
                // Add XR Interactable component for interaction
                gobj.AddComponent<XRSimpleInteractable>(); 
                gobj.name = part.name;

                // Add collider to XR Interactable
                MeshCollider collider = gobj.AddComponent<MeshCollider>();
                var interactable = gobj.GetComponent<XRSimpleInteractable>();
                interactable.colliders.Add(collider);

                collider.sharedMesh = gobj.GetComponentInChildren<MeshFilter>().sharedMesh;
                part_objects[i-num_hrows] = gobj;

                if (part.category == "Valve_Interactive") {
                    gobj.transform.localScale = new Vector3(10f,10f,10f);
                    if (part.type == "ValveControl") {
                        gobj.GetComponentInChildren<TMPro.TMP_Text>().text = gobj.name;
                        CanvasGroup canvas = gobj.GetComponentInChildren<CanvasGroup>();
                        canvas.interactable = false;
                        canvas.blocksRaycasts = false;
                        canvas.alpha = 0f;
                        canvas.gameObject.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
                        var button_sp = GameObject.Find("Button_SP_Confirm").GetComponent<Button>();
                            //GetComponentInChildren<UnityEngine.UI.Button>();

                        int idx = -1;
                        if (part.name == "FCV01") {
                            button_sp.onClick.AddListener(delegate{OnOperatingPointChange(gobj, FIC01_OP_manu_index);});
                            idx = FIC01_OP_manu_index;
                        }
                        if (part.name == "PCV02") {
                            button_sp.onClick.AddListener(delegate{OnOperatingPointChange(gobj, PIC02_OP_manu_index);});
                            idx = PIC02_OP_manu_index;
                        }
                        TMPro.TMP_Text value_display = null;
                        foreach (TMPro.TMP_Text text in gobj.GetComponentsInChildren<TMPro.TMP_Text>()) {
                            if (text.name == "Value_SP") {
                                value_display = text;
                                break;
                            } 
                        }
                        if (idx >= 0) {
                            if (value_display != null) {
                                float value = sim_data[idx*sim.steps_max+sim.steps_computed]; // originally sim_data
                                value_display.text = value.ToString("0.000");
                            }
                            else {
                                if (value_display == null) Debug.Log("ERROR: Value_SP not found");
                            }
                        }
                        else {
                            Debug.Log(String.Format("ERROR: Control Valve index not specified: {0}", part.name));
                        }
                    }
                    else {
                        try {
                            GameObject element_obj = (GameObject)Instantiate(
                                prefabs[prefab_dict[part.type+"_Element"]],
                                gobj.transform
                            );
                            element_obj.name = "Element";
                        }
                        catch{
                        }
                    }
                }
                else {
                    gobj.transform.localScale = new Vector3(0.1f,0.1f,0.1f);
                }
                // Add OutlineEffect (has to be run after any additional children are added to the part)
                foreach (MeshRenderer mesh_renderer in gobj.GetComponentsInChildren<MeshRenderer>()) {
                    OutlineEffect effect = mesh_renderer.gameObject.AddComponent<OutlineEffect>();
                    effect.enabled = false;
                }
            }

            GameObject frame = GameObject.Find("Frame");

            pos.transform.position = frame.transform.position;

        }

        // Setting up part states
        {   // Configuration valves
            string[] configuration_valve_names = {
                "V01", "V04", "V05", "V06", "V07", "V11",
                "V12", "V13", "V14", "V15", "V16", "V17",
                "V18", "V19", "V21", "V23",
                "VP01", "VP02", "VP03", "VP04", "VP05", "VP06", "VP07", "VP08", "VP09",
            };
            foreach (string valve_name in configuration_valve_names) {
                configuration_valves[valve_name] = false;
            }
        }
        {   // Manual proportional valves
            string[] manual_proportional_valve_names = {
                "V08", "V09", "V10", "V20", "V22", "V24",
            };
            foreach (string valve_name in manual_proportional_valve_names) {
                proportional_valves[valve_name] = 0.0f;
            }
        }
        {   // Pumps
            string[] pump_names = {
                "P01", "P02", "P03"
            };
            foreach (string pump_name in pump_names) pumps[pump_name] = false;
        }

        {   // Tanks
            vessels["B01"] = new MPP_Vessel() {
                volume     = 0.0f,
                max_volume = 180.0f,
                max_height = 320.0f,
                radius     = 560.0f,
            };
            vessels["B02"] = new MPP_Vessel() {
                volume     = 0.0f,
                max_volume = 120.0f,
                max_height = 340.0f,
                radius     = 440.0f,
            };
            vessels["B03"] = new MPP_Vessel() {
                volume     = 0.0f,
                max_volume = 120.0f,
                max_height = 340.0f,
                radius     = 440.0f,
            };
            vessels["B04"] = new MPP_Vessel() {
                volume     = 0.0f,
                max_volume = 120.0f,
                max_height = 340.0f,
                radius     = 440.0f,
            };
        }

        {   // Proportional control valves
            string[] proportional_control_valve_names = {
                "FCV01", "PCV02"
            };
            foreach (string valve_name in proportional_control_valve_names) {
                MPP_PCV_State valve_state = new MPP_PCV_State() {};
                valve_state.automatic_manual = false;
                valve_state.operating_point_manual = 100f;
                // @Incomplete1(isuru 20/05/2024): Hack, can pull in from the graph instead later on
                if      (valve_name == "FCV01") {
                    valve_state.setpoint = 0f;
                    valve_state.process_variable_source = "FIT01";
                }
                else if (valve_name == "PCV02") {
                    valve_state.setpoint = 0f;
                    valve_state.process_variable_source = "PT02";
                }
                else {
                    valve_state.process_variable_source = null;
                }
                proportional_control_valves[valve_name] = valve_state;
            }
        }

        {   // Alarms
            alarms["ALM_LSL01"] = false;
            alarms["ALM_LSH02"] = false;
            alarms["ALM_LSH03"] = false;
            alarms["ALM_TMP"] = false;
        }

        // Setting up uneditable parts
        foreach (string uneditable_part_name in uneditable_parts) {
            MeshRenderer[] renderers = GameObject.Find(uneditable_part_name).GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer renderer in renderers) {
                // renderer.material.color = uneditable_colour;
                renderer.material = uneditable_material;
            }
        }
        //External
        // UF
        configuration_valves["VP01"] = false;
        configuration_valves["VP02"] = false;
        configuration_valves["VP04"] = true;
        configuration_valves["VP05"] = true;
        configuration_valves["VP06"] = false;
        configuration_valves["VP07"] = false;
        configuration_valves["VP08"] = true;
        configuration_valves["VP09"] = true;
        // RO
        proportional_valves["V08"]   = 0.0f;
        proportional_valves["V09"]   = 0.0f;
        proportional_valves["V10"]   = 0.0f;
        configuration_valves["V11"]  = false;
        configuration_valves["V14"]  = false;
        configuration_valves["V23"]  = false;
        
        // Making sure visuals match their initial setting
        foreach (var item in configuration_valves) turn_configuration_valve(GameObject.Find(item.Key));
        foreach (var item in pumps) GameObject.Find(item.Key).GetComponentInChildren<MeshRenderer>().material = item.Value ? pump_active_material : pump_inactive_material;


        // Setting up sensor interfaces
        FIT01_sensor = GameObject.Find("FIT01");
        FIT02_sensor = GameObject.Find("FIT02");
        FIT03_sensor = GameObject.Find("FIT03");
        FIT04_sensor = GameObject.Find("FIT04");
        FIT05_sensor = GameObject.Find("FIT05");
        PT01_sensor  = GameObject.Find("PT01");
        PT02_sensor  = GameObject.Find("PT02");
        PT03_sensor  = GameObject.Find("PT03");
        PT04_sensor  = GameObject.Find("PT04");
        FindTextField(FIT01_sensor, "Text_Name").GetComponent<TMPro.TMP_Text>().text = "FIT01";
        FindTextField(FIT02_sensor, "Text_Name").GetComponent<TMPro.TMP_Text>().text = "FIT02";
        FindTextField(FIT03_sensor, "Text_Name").GetComponent<TMPro.TMP_Text>().text = "FIT03";
        FindTextField(FIT04_sensor, "Text_Name").GetComponent<TMPro.TMP_Text>().text = "FIT04";
        FindTextField(PT01_sensor,  "Text_Name").GetComponent<TMPro.TMP_Text>().text = "PT01";
        FindTextField(PT02_sensor,  "Text_Name").GetComponent<TMPro.TMP_Text>().text = "PT02";
        FindTextField(PT03_sensor,  "Text_Name").GetComponent<TMPro.TMP_Text>().text = "PT03";
        FindTextField(PT04_sensor,  "Text_Name").GetComponent<TMPro.TMP_Text>().text = "PT04";
        FIT01_value_field = FindTextField(FIT01_sensor, "Value_PV").GetComponent<TMPro.TMP_Text>();
        FIT02_value_field = FindTextField(FIT02_sensor, "Value_PV").GetComponent<TMPro.TMP_Text>();
        FIT03_value_field = FindTextField(FIT03_sensor, "Value_PV").GetComponent<TMPro.TMP_Text>();
        FIT04_value_field = FindTextField(FIT04_sensor, "Value_PV").GetComponent<TMPro.TMP_Text>();
        PT01_value_field  = FindTextField(PT01_sensor,  "Value_PV").GetComponent<TMPro.TMP_Text>();
        PT02_value_field  = FindTextField(PT02_sensor,  "Value_PV").GetComponent<TMPro.TMP_Text>();
        PT03_value_field  = FindTextField(PT03_sensor,  "Value_PV").GetComponent<TMPro.TMP_Text>();
        PT04_value_field  = FindTextField(PT04_sensor,  "Value_PV").GetComponent<TMPro.TMP_Text>();

        pathways = import_pathways(PathwaysTxtFile);
    }
    
    // (isuru): Up until where the conversion happens, flow rates are measured in L/min
    void StepSimulation(ref float[] sim_data, int t) {

        // ZOH control data
        if (t > 0) {
            for (int i = 0; i < n_control; i++) {
                sim_data[i*sim.steps_max+t] = sim_data[i*sim.steps_max+t-1];
            }
        }

        // Inputs
        float fcv01_opening = configuration_valves["V01"] ? 1.0f : proportional_control_valves["FCV01"].operating_point_manual;
        float pcv02_opening = configuration_valves["V06"] ? 1.0f : proportional_control_valves["PCV02"].operating_point_manual;
        float fit01 = 0.0f;
        float pt01  = 0.0f;
        float pt02  = 0.0f;
        // Outputs
        float fit02 = 0.0f;
        float fit03 = 0.0f;

        bool p01  = pumps["P01"];
        bool v04  = configuration_valves["V04"];
        bool v05  = configuration_valves["V05"];
        bool v12  = configuration_valves["V12"];
        bool v13  = configuration_valves["V13"];
        float v20  = proportional_valves["V20"];
        bool vp03 = configuration_valves["VP03"];
        bool vp08 = configuration_valves["VP08"];
        bool vp09 = configuration_valves["VP09"];

        float N1P, N1F, N1T, N1Tb, N1C;
        float N2P, N2F, N2T, N2Tb, N2C;
        float N3P, N3F, N3T, N3Tb, N3C;
        float N4P, N4F, N4T, N4Tb, N4C;
        float N5P, N5F, N5T, N5Tb, N5C;
        float N6P, N6T, N6Tb, N6C;
        float N6F = 0;
        float N7P, N7F, N7T, N7Tb, N7C;
        float N8P, N8F, N8T, N8Tb, N8C;
        float N9P, N9F, N9T, N9Tb, N9C;
        float N10P, N10F, N10T, N10Tb, N10C;
        float N11P, N11F, N11T, N11Tb, N11C;

        if (p01) {
            if (vp03) {
                float V20 = proportional_valves["V20"]*6; // 6 turns to fully open V20
                float FCV01_OP = fcv01_opening;
                float Temp_feed = 20;
                float Turb_feed = 0;

                // Calculations for node 1
                N1P = 0;
                // N1F = YET TO BE MODELLED 
                // if the temperature is set to 1 (or 0), the temperature is automatically set to 20 degrees
                N1T = 20;
                N1Tb = 0;
                N1C = 0;

                // Calculations for node 2
                N2P = 2.5f;
                N2F = 1.02125f + 0.000142592f*MathF.Sqrt(1.85816f*MathF.Pow(10,9) - 3.5065f*MathF.Pow(10,6)*N2P);
                N2T = N1T + 0.2f;
                N2Tb = N1Tb;
                N2C = N1C;

                N1F = N2F;

                // Calculations for node 4
                N4P = (-0.1154f*V20-1.3899f)+(V20*0.055f+0.6557f)*MathF.Log(FCV01_OP);

                N4F = -214.07878f*MathF.Sqrt(0.91444f*MathF.Sin(8.80024909973145f*FCV01_OP + 4.41116f) - 0.01477f*MathF.Tanh(0.84815f*V20 - 4.19998f) + 1) - 0.409f*MathF.Sin(0.19655f*MathF.Sin(0.652f*V20 - 1.0f) + 404.40588f*MathF.Sin(8.80008f*FCV01_OP - 6.58619f) + 128.29628f) + 2.98135f*MathF.Tanh(169.27702f*MathF.Sin(8.7983f*FCV01_OP + 5.98104f) - 1.92663f*MathF.Tanh(0.60911f*V20 - 3.06801f) + 46.17941f) + 81.11295f;

                N4T = N2T;
                N4Tb = N2Tb; 
                N4C = N2C;

                // Calculations for node 3
                N3P = 0; 
                N3F = N2F-N4F;
                N3T = N2T;
                N3Tb = N2Tb;
                N3C = N2C;

                // Calculations for node 6
                N6P = 0.7279f*N4P + 0.1181f;

                N6F = 0.8795f*N4F + 0.5334f;

                N6T = N4T;
                N6C = N4C;

                // Calculations for node 5
                N5P = 0;
                N5F = N4F-N6F;
                N5T = N4T;
                N5Tb = 0; // FOR ALL CURRENT FEEDSTOCKS...
                N5C = N4C;

                
                N6Tb = -5003336775.21898f*MathF.Exp(35.99665f*MathF.Sin(0.00012f*N4F + 3.57396f) - 0.11102f*MathF.Sin(0.49804f*N4P + 7.71306f) - 0.0476f*MathF.Sin(4.08871f*N5F + 5.28088f) + 0.21604f*MathF.Tanh(1.53523f*N6P - 2.39585f)) - 153.26544f*MathF.Sin(66.87566f*MathF.Sin(0.65202f*N4P + 4.35689f) - 5.9102f*MathF.Sin(6.17233f*N5F + 2.9733f) - 26.96482f*MathF.Tanh(0.21697f*N4F - 3.41842f) + 38.46969f*MathF.Tanh(1.99819f*N6P - 2.66458f) + 18.37512f) - 1701.08105f*MathF.Sin(0.02915f*MathF.Sin(3.26145f*N6P - 5.3132f) + 0.11548f*MathF.Sin(2.85753f*N5F - 2.44759f) + 86.76361f*MathF.Tanh(0.01713f*N4F + 2.40432f) + 0.04076f*MathF.Tanh(0.2551f*N4P - 0.97282f) - 94.32675f) + 56.98187f;

                // Calculations for node 7
                N7P = (N6P-(1000*(1-210*MathF.Pow(10,-6)*(N6T-273)))*((35840*N6F)/(81*3.1415f*MathF.Pow(N6T,0.88f))+(9.81f/2)))/100000; // poiseuille's equation


                N7F = N6F;
                N7T = N6T;
                N7Tb = N6Tb; // Based on KAN model
                N7C = N6C;

                // Calculations for node 8
                N8P = 0;
                N8F = N7F;
                N8T = N7T - 0.3508f*N7F+10.312f;
                N8Tb = N7Tb;
                N8C = N7C;

                // Calculations for node 9
                N9P = 0.5f; // Random number for now. 
                N9F = 7.5f;
                N9T = 20; // assuming room temeprature water
                N9Tb = 0; // tap water 
                N9C = 0; // Tap water
                
                // Calculations for node 10
                N10P = 0;
                N10F = N9F;
                N10T = N7F*(0.3508f*N7F-10.312f)/N9F+N9T;
                N10Tb = 0; // Tap water
                N10C = 0; // Tap water
                
                // Calculations for node 11
                N11P = 0;
                N11F = N7F;
                N11T = N5T;
                N11Tb = N5Tb;
                N11C = N5C;

                // @Incomplete(isuru 05/09/2024): Flipping signs here because otherwise the plot is upside down. Not sure why---whether it's an error in the process model or in our plotting
                fit01 = -N4F;
                fit02 = -N7F;
                fit03 = -N5F;
                pt01  = -N4P;
                pt02  = -N6P;

                sim_data[sim.steps_max*FIT01_index + t] = fit01;
                sim_data[sim.steps_max*FIT02_index + t] = fit02;
                sim_data[sim.steps_max*FIT03_index + t] = fit03;
                sim_data[sim.steps_max*PT01_index  + t] = pt01;
                sim_data[sim.steps_max*PT02_index  + t] = pt02;
            }
        }
        // (isuru): Converting from L/min to L/s
        fit01 /= 60f;
        fit02 /= 60f;
        fit03 /= 60f;

        float dV_B01 = 0f;
        dV_B01 -= fit01;
        // dV_B01 += proportional_valves["V20"]*5f; (isuru): Already accounted for by subtracting this amount from fit01
        if (switches[0].GetComponent<WaterSupply>().IsActivated() && configuration_valves["V18"] && configuration_valves["V19"]) dV_B01 += 2f;
        if (!configuration_valves["V07"])                                                        dV_B01 += fit02;   // for wawter from retentate to flow into the tank
        if (configuration_valves["V17"])                                                         dV_B01 -= 0.1f;    // for draining
        // @Incomplete1(isuru 26/05/2024): EV01-P03-V24 line
        vessels["B01"].volume = vessels["B01"].volume + dV_B01*sim.step_length;

        float dV_B02 = 0f;
        dV_B02 += fit03;
        if (configuration_valves["V16"]) dV_B02 -= 0.1f;
        vessels["B02"].volume = vessels["B02"].volume + dV_B02*sim.step_length;

        float dV_B03 = 0f;
        if (configuration_valves["V07"]) dV_B03 += fit02;
        if (configuration_valves["V15"]) dV_B03 -= 0.1f;
        vessels["B03"].volume = vessels["B03"].volume + dV_B03*sim.step_length;

        alarms["ALM_LSL01"] = vessels["B01"].volume < 20.0f;
        alarms["ALM_LSH02"] = vessels["B02"].volume > 80.0f;
        alarms["ALM_LSH03"] = vessels["B03"].volume > 80.0f;
        // @Incomplete(isuru 05/09/2024): ALM_TMP
 
        // Shut down P01 if an alarm goes off
        if (alarms["ALM_LSL01"] || alarms["ALM_LSH02"] || alarms["ALM_LSH03"] || alarms["ALM_TMP"]) set_pump("P01", false);
    }

    void QueueEvent(MPP_Event ev, ref List<MPP_Event> event_queue, ref int event_queue_index) {
        // Overwrite any later events in the queue
        event_queue.RemoveRange(event_queue_index+1, event_queue.Count - (event_queue_index+1));
        // Queue up the new event
        event_queue.Add(ev);
        event_queue_index += 1;
    }

    // @IncompleteUnRedo(isuru 27/05/2024)
    void UndoEvent(ref MPP_Simulation_State sim, ref List<MPP_Event> event_queue, ref int event_queue_index) {
        if (event_queue_index >= 0) {
            MPP_Event ev = event_queue[event_queue_index];
            
            sim.seconds_elapsed = ev.seconds_elapsed;
            sim.steps_computed  = ev.t_sim_step;
            sim.steps_elapsed   = ev.t_sim_step;
            fsm_state = ev.fsm_state;

            switch (ev.type) {
                case MPP_Event_Type.configuration_valve_interaction: {
                    configuration_valves[ev.name] = ev.before_bool;
                } break;
                case MPP_Event_Type.proportional_valve_interaction: {
                    proportional_valves[ev.name] = ev.before_float;
                } break;
                case MPP_Event_Type.proportional_control_valve_interaction: {
                    MPP_PCV_State valve_state = proportional_control_valves[ev.name];
                    valve_state.operating_point_manual = ev.before_float;
                    proportional_control_valves[ev.name] = valve_state;
                } break;
                case MPP_Event_Type.pump_interaction: {
                    pumps[ev.name] = ev.before_bool;
                } break;
                default: {
                    Debug.Log("HUH?");
                } break;
            }

            event_queue_index -= 1;
        }
    }

    // @IncompleteUnRedo(isuru 27/05/2024)
    void RedoEvent(ref MPP_Simulation_State sim, ref List<MPP_Event> event_queue, ref int event_queue_index) {
        if (event_queue_index < event_queue.Count) {

            MPP_Event ev = event_queue[event_queue_index];
            
            sim.seconds_elapsed = ev.seconds_elapsed;
            sim.steps_computed  = ev.t_sim_step;
            sim.steps_elapsed   = ev.t_sim_step;
            fsm_state = ev.fsm_state;

            switch (ev.type) {
                case MPP_Event_Type.configuration_valve_interaction: {
                    configuration_valves[ev.name] = ev.after_bool;
                } break;
                case MPP_Event_Type.proportional_valve_interaction: {
                    proportional_valves[ev.name] = ev.after_float;
                } break;
                case MPP_Event_Type.proportional_control_valve_interaction: {
                    MPP_PCV_State valve_state = proportional_control_valves[ev.name];
                    valve_state.operating_point_manual = ev.after_float;
                    proportional_control_valves[ev.name] = valve_state;
                } break;
                case MPP_Event_Type.pump_interaction: {
                    pumps[ev.name] = ev.after_bool;
                } break;
                default: {
                    Debug.Log("HUH?");
                } break;
            }

            event_queue_index += 1;
        }
    }

    void Update() {
        float dt = demo_speed * Time.deltaTime;
        seconds_elapsed += dt;
        sim.steps_elapsed = Math.Min((int)(seconds_elapsed/sim.step_length), sim.steps_max);

        // Get current controller
        ActionBasedController currentController = null;

        if (leftController.activateAction.action.WasPressedThisFrame()) currentController = leftController;
        else if (rightController.activateAction.action.WasPressedThisFrame()) currentController = rightController;
        else currentController = null;

        // XR Shortcuts

        // left grip - toggle highlights
        if (leftController.selectAction.action.WasPressedThisFrame()) highlight_instruction_parts = !highlight_instruction_parts;
        
        if (Input.GetKeyDown(KeyCode.JoystickButton0))
        {
            // Undo
            UndoEvent(ref sim, ref event_queue, ref event_queue_index);
        }
        if (Input.GetKeyDown(KeyCode.JoystickButton1))
        {
            // Redo
            RedoEvent(ref sim, ref event_queue, ref event_queue_index);
        }


        // Keyboard shortcuts
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) {
            // Toggle highlights on parts named in instructions
            if (Input.GetKeyDown(KeyCode.H)) {
                highlight_instruction_parts = !highlight_instruction_parts;
            }

            // Undo and redo
            if (Input.GetKeyDown(KeyCode.Z)) {
                UndoEvent(ref sim, ref event_queue, ref event_queue_index);
            }
            if (Input.GetKeyDown(KeyCode.Y)) {
                RedoEvent(ref sim, ref event_queue, ref event_queue_index);
            }
        }
        
        if (sim.steps_computed > sim.steps_elapsed) sim.steps_computed = sim.steps_elapsed;
        for (; sim.steps_computed < sim.steps_elapsed; sim.steps_computed++) {
            if (event_queue_index+1 < event_queue.Count) {
                if (event_queue[event_queue_index+1].t_sim_step == sim.steps_computed) {
                    RedoEvent(ref sim, ref event_queue, ref event_queue_index);
                }
            }
#if !TEST_PLOT
            StepSimulation(ref sim_data, sim.steps_computed);
#endif
        }
        GameObject.Find("PlottingCanvas").GetComponentInChildren<Plotting>().SetAllDirty();
        
        //originally sim_data
        FIT01_value_field.text = sim_data[sim.steps_max * FIT01_index + sim.steps_computed-1].ToString("0.0"); 
        FIT02_value_field.text = sim_data[sim.steps_max * FIT02_index + sim.steps_computed-1].ToString("0.0");
        FIT03_value_field.text = sim_data[sim.steps_max * FIT03_index + sim.steps_computed-1].ToString("0.0");
        PT01_value_field.text  = sim_data[sim.steps_max * PT01_index  + sim.steps_computed-1].ToString("0.00");
        PT02_value_field.text  = sim_data[sim.steps_max * PT02_index  + sim.steps_computed-1].ToString("0.00");

        vessels["B01"].volume = Mathf.Clamp(vessels["B01"].volume, 0f, vessels["B01"].max_volume*1.01f);
        vessels["B02"].volume = Mathf.Clamp(vessels["B02"].volume, 0f, vessels["B02"].max_volume*1.01f);
        vessels["B03"].volume = Mathf.Clamp(vessels["B03"].volume, 0f, vessels["B03"].max_volume*1.01f);
        vessels["B04"].volume = Mathf.Clamp(vessels["B04"].volume, 0f, vessels["B04"].max_volume*1.01f);

        Transform cylinder_b01 = GameObject.Find("B01").transform.Find("Cylinder");
        Transform cylinder_b02 = GameObject.Find("B02").transform.Find("Cylinder");
        Transform cylinder_b03 = GameObject.Find("B03").transform.Find("Cylinder");
        Transform cylinder_b04 = GameObject.Find("B04").transform.Find("Cylinder");
        float scale_b01 = Mathf.Max(vessels["B01"].max_height * vessels["B01"].volume / vessels["B01"].max_volume, 1f);
        float scale_b02 = Mathf.Max(vessels["B02"].max_height * vessels["B02"].volume / vessels["B02"].max_volume, 1f);
        float scale_b03 = Mathf.Max(vessels["B03"].max_height * vessels["B03"].volume / vessels["B03"].max_volume, 1f);
        float scale_b04 = Mathf.Max(vessels["B04"].max_height * vessels["B04"].volume / vessels["B04"].max_volume, 1f);
        cylinder_b01.localPosition = new Vector3(0f, 20f+scale_b01, 0f);
        cylinder_b02.localPosition = new Vector3(0f, 20f+scale_b02, 0f);
        cylinder_b03.localPosition = new Vector3(0f, 20f+scale_b03, 0f);
        cylinder_b04.localPosition = new Vector3(0f, 20f+scale_b04, 0f);
        cylinder_b01.localScale = new Vector3(vessels["B01"].radius, scale_b01, vessels["B01"].radius);
        cylinder_b02.localScale = new Vector3(vessels["B02"].radius, scale_b02, vessels["B02"].radius);
        cylinder_b03.localScale = new Vector3(vessels["B03"].radius, scale_b03, vessels["B03"].radius);
        cylinder_b04.localScale = new Vector3(vessels["B04"].radius, scale_b04, vessels["B04"].radius);

        if (vessels["B01"].volume > vessels["B01"].max_volume) {
            // @Incomplete1(isuru 26/05/2024): Overflowing
        }
        if (vessels["B02"].volume > vessels["B02"].max_volume) {
            // @Incomplete1(isuru 26/05/2024): Overflowing
        }
        if (vessels["B03"].volume > vessels["B03"].max_volume) {
            // @Incomplete1(isuru 26/05/2024): Overflowing
        }
        if (vessels["B04"].volume > vessels["B04"].max_volume) {
            // @Incomplete1(isuru 26/05/2024): Overflowing
        }

        GameObject.Find("V12").transform.Find("Cylinder").GetComponent<MeshRenderer>().enabled = configuration_valves["V12"];
        GameObject.Find("V13").transform.Find("Cylinder").GetComponent<MeshRenderer>().enabled = configuration_valves["V13"];
        GameObject.Find("V14").transform.Find("Cylinder").GetComponent<MeshRenderer>().enabled = configuration_valves["V14"];
        GameObject.Find("V15").transform.Find("Cylinder").GetComponent<MeshRenderer>().enabled = configuration_valves["V15"] && (vessels["B03"].volume > 1f);
        GameObject.Find("V16").transform.Find("Cylinder").GetComponent<MeshRenderer>().enabled = configuration_valves["V16"] && (vessels["B02"].volume > 1f);
        GameObject.Find("V17").transform.Find("Cylinder").GetComponent<MeshRenderer>().enabled = configuration_valves["V17"] && (vessels["B01"].volume > 1f);
        GameObject.Find("V23").transform.Find("Cylinder").GetComponent<MeshRenderer>().enabled = configuration_valves["V23"] && (vessels["B04"].volume > 1f);

        // Updating state machine state and do instruction stuff
        bool procedure_state_changed = procedure.update_state(
            configuration_valves,
            proportional_valves,
            proportional_control_valves,
            pumps,
            vessels,
            alarms
        );
        if (procedure_state_changed) {
            MPP_Instruction instruction = procedure.get_instruction();
            // Set instruction text
            instruction_text.GetComponent<TMPro.TMP_Text>().text = instruction.text;
            // Clear currently highlighted parts
            foreach (GameObject p in highlighted_parts) {
                foreach (OutlineEffect effect in p.GetComponentsInChildren<OutlineEffect>()) {
                    effect.enabled = false;
                }
            }
            foreach (GameObject p in highlighted_parts_red) {
                foreach (OutlineEffect effect in p.GetComponentsInChildren<OutlineEffect>()) {
                    effect.enabled = false;
                }
            }
            // Set parts referenced in instruction to be highlighted
            highlighted_parts.Clear();
            foreach (string part_name in instruction.referenced_parts) {
                GameObject part = GameObject.Find(part_name);
                highlighted_parts.Add(part);
            }
            highlighted_parts_red.Clear();
            foreach (string part_name in instruction.referenced_parts_red) {
                Debug.Log(part_name);
                GameObject part = GameObject.Find(part_name);
                highlighted_parts_red.Add(part);
            }
        }
        // Highlight parts referenced in instruction
        foreach (GameObject p in highlighted_parts) {
            foreach (OutlineEffect effect in p.GetComponentsInChildren<OutlineEffect>()) {
                effect.color = 0;
                effect.enabled = highlight_instruction_parts;
            }
        }
        foreach (GameObject p in highlighted_parts_red) {
            foreach (OutlineEffect effect in p.GetComponentsInChildren<OutlineEffect>()) {
                effect.color = 1;
                effect.enabled = highlight_instruction_parts;
            }
        }

        if (event_system.currentSelectedGameObject == null){
            // Movement
            float frametime = Time.deltaTime;
            if (frametime > 1/30.0f) frametime = 1/30.0f;
            float v_translation = Input.GetAxis("Vertical") * speed * frametime;
            float h_translation = Input.GetAxis("Horizontal") * speed * frametime;
            if (camera_free_not_constrained) {
                Camera.main.transform.Translate(h_translation, 0, v_translation);
            }
            else {
                float theta = Camera.main.transform.localEulerAngles.y;
                float sintheta = Mathf.Sin(Mathf.PI*theta/180);
                float costheta = Mathf.Cos(Mathf.PI*theta/180);
                Camera.main.transform.Translate(
                    h_translation*costheta + v_translation*sintheta,
                    camera_height - Camera.main.transform.position.y,
                    -h_translation*sintheta + v_translation*costheta,
                    Space.World
                );
            }

            if (selected_prop_valve != null) {
                float dval = (Input.mousePosition.y - prop_valve_edit_y) / 1000.0f;
                float t = Mathf.Clamp(prop_valve_edit_val + dval, 0.0f, 1.0f);
                proportional_valves[selected_prop_valve_name] = t;
                Transform element_transform = selected_prop_valve.transform.Find("Element");
                if (selected_prop_valve_name == "V09") element_transform.localEulerAngles = new Vector3(0, 0, -90*t);
                else                                   element_transform.localEulerAngles = new Vector3(0, 0, 4320*t);
                prop_valve_visual.GetComponent<MeshFilter>().mesh.vertices = new Vector3[6]{
                    new Vector3(0,0,0),
                    new Vector3(-pvv_width,pvv_height,0),
                    new Vector3(-pvv_width,0,0),
                    new Vector3(0,0,0.01f),
                    new Vector3(-t*pvv_width, t*pvv_height, 0.01f),
                    new Vector3(-t*pvv_width, 0, 0.01f),
                };
            }

            // Clicking
            if (Input.GetMouseButtonDown(0) || currentController) {
                if (selected_part != null) {
                    foreach (MSPP_Part part in part_definitions) {
                        if (part.name == selected_part.name) {
                            if (part.type == "ValveControl") {
                                CanvasGroup canvas = selected_part.GetComponentInChildren<CanvasGroup>();
                                canvas.interactable = false;
                                canvas.blocksRaycasts = false;
                                canvas.alpha = 0f;
                            }
                            break;
                        }
                    }
                }

                RaycastHit hit;
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

                //XR controller ray 
                // TODO: Add POKE interaction
                XRRayInteractor xRRayInteractor = currentController.GetComponentInChildren<XRRayInteractor>();
                if (Physics.Raycast(ray, out hit)) selected_part = hit.collider.gameObject;
                else if (xRRayInteractor.TryGetCurrent3DRaycastHit(out hit)) selected_part = hit.collider.gameObject;
                else selected_part = null;

                // Debug
                if (selected_part != null)
                {
                    Debug.Log(String.Format("Selected part: {0}", selected_part.name));
                }

                if (selected_part != null) {
                    bool selected_part_is_editable = true;
                    foreach (string uneditable_part in uneditable_parts) {
                        if (selected_part.name == uneditable_part) {
                            selected_part_is_editable = false;
                            break;
                        }
                    }

                    // Cannot actuate pneumatic valves if the compressed air supply is deactivated
                    if (!switches[1].GetComponent<WaterSupply>().activated) {
                        foreach (string pneumatic_valve in pneumatic_valves) {
                            if (selected_part.name == pneumatic_valve) {
                                selected_part_is_editable = false;
                                break;
                            }
                        }
                        foreach (string pneumatic_valve in threeway_pneumatic_valves) {
                            if (selected_part.name == pneumatic_valve) {
                                selected_part_is_editable = false;
                                break;
                            }
                        }
                    }

                    // Cannot actuate electric valves if the power supply is deactivated
                    if (!switches[2].GetComponent<WaterSupply>().activated) {
                        foreach (string electric_valve in electric_valves) {
                            if (selected_part.name == electric_valve) {
                                selected_part_is_editable = false;
                                break;
                            }
                        }
                    }
                    
                    if (selected_part_is_editable) {
                        foreach (GameObject sw in switches) {
                            if (sw == selected_part) {
                                goto Selected_Part_Interaction_Handled;
                            }
                        }

                        foreach (MSPP_Part part in part_definitions) {
                            if (part.name == selected_part.name) {
                                if (part.type == "ValveControl") {
                                    CanvasGroup canvas = selected_part.GetComponentInChildren<CanvasGroup>();
                                    canvas.interactable = true;
                                    canvas.blocksRaycasts = true;
                                    canvas.alpha = 1f;
                                }
                                else if (part.category == "Valve_Interactive") {
                                    try {
                                        bool prev_value = configuration_valves[selected_part.name];
                                        configuration_valves[selected_part.name] = !prev_value;

                                        // Creating event in event queue
                                        MPP_Event ev = new MPP_Event();
                                        ev.type = MPP_Event_Type.configuration_valve_interaction;
                                        ev.seconds_elapsed = seconds_elapsed;
                                        ev.t_sim_step = sim.steps_computed;
                                        ev.fsm_state = fsm_state;
                                        ev.name = part.name;
                                        ev.before_bool = prev_value;
                                        ev.after_bool  = !prev_value;
                                        QueueEvent(ev, ref event_queue, ref event_queue_index);

                                        // Handling linked parts
                                        bool part_is_a_linked_part = false;
                                        bool positive_relationship = false;
                                        string other_part_of_the_link = "ERROR";
                                        foreach (string[] links in linked_parts) {
                                            if (links[0] == selected_part.name) {
                                                part_is_a_linked_part = true;
                                                other_part_of_the_link = links[1];
                                                positive_relationship = (links[2] == "+");
                                                if (positive_relationship) configuration_valves[other_part_of_the_link] =  configuration_valves[selected_part.name];
                                                else                       configuration_valves[other_part_of_the_link] = !configuration_valves[selected_part.name];
                                                turn_configuration_valve(GameObject.Find(other_part_of_the_link));
                                                break;
                                            }
                                        }
                                    }
                                    catch {
                                        try {
                                            float val = proportional_valves[selected_part.name];
                                            selected_prop_valve = GameObject.Find(selected_part.name);
                                            selected_prop_valve_name = selected_part.name;
                                            prop_valve_edit_val = val;
                                            prop_valve_edit_y = Input.mousePosition.y;
                                            prop_valve_visual.transform.position = selected_prop_valve.transform.position + new Vector3(pvv_width/2,5,2);
                                            prop_valve_visual.SetActive(true);
                                        }
                                        catch{
                                            Debug.Log(String.Format("ERROR: Could not modify valve state {0}", selected_part.name));
                                        }
                                    }

                                    turn_configuration_valve(selected_part);
                                }
                                else if (part.category == "Component_Interactive") {
                                    // Currently just pumps
                                    // TODO: Simulate pump interaction
                                    bool prev_value = pumps[selected_part.name];
                                    bool new_value = !prev_value;
                                    set_pump(selected_part.name, new_value);

                                    // Creating event in event queue
                                    MPP_Event ev = new MPP_Event();
                                    ev.type = MPP_Event_Type.pump_interaction;
                                    ev.seconds_elapsed = seconds_elapsed;
                                    ev.t_sim_step = sim.steps_computed;
                                    ev.fsm_state = fsm_state;
                                    ev.name = part.name;
                                    ev.before_bool = prev_value;
                                    ev.after_bool  = new_value;
                                    QueueEvent(ev, ref event_queue, ref event_queue_index);
                                }
                                goto Selected_Part_Interaction_Handled;
                            }
                        }
                        Selected_Part_Interaction_Handled:;
                    }
                }
            }
            if (Input.GetMouseButtonUp(0) || (currentController && currentController.activateAction.action.WasReleasedThisFrame())) {
                if (selected_prop_valve != null) {
                    Debug.Log(String.Format("Released {0}", selected_prop_valve_name));
                    selected_prop_valve = null;
                    prop_valve_visual.SetActive(false);

                    // Creating event in event queue
                    MPP_Event ev = new MPP_Event{};
                    ev.type = MPP_Event_Type.proportional_valve_interaction;
                    ev.seconds_elapsed = seconds_elapsed;
                    ev.t_sim_step = sim.steps_computed;
                    ev.fsm_state = fsm_state;
                    ev.name = selected_prop_valve_name;
                    ev.before_float = prop_valve_edit_val;
                    ev.after_float = proportional_valves[selected_prop_valve_name];
                    QueueEvent(ev, ref event_queue, ref event_queue_index);
                }
            }

            // Mouselook
            if (Input.GetMouseButton(1)) {
                float v_rotation = -Input.GetAxis("Mouse Y")  * lookspeed * frametime;
                float h_rotation = Input.GetAxis("Mouse X")  * lookspeed * frametime;
                Vector3 curr_euler_angles = Camera.main.transform.localEulerAngles;
                float v_angle = curr_euler_angles.x+v_rotation;
                if (v_angle > 90 && v_angle < 180) v_angle = 90;
                else if (v_angle > 180 && v_angle < 270) v_angle = 270;
                Camera.main.transform.rotation = Quaternion.Euler(
                    v_angle,
                    curr_euler_angles.y+h_rotation,
                    0
                );
            }
        }

        if (highlight_flow_lines) {
            // Update flow line highlight
            // @Incomplete0(isuru 29/05/2024): This sort of works but it doesn't take into account direction of flow which means it'll show flow going from B01 into E01!
            // Maybe have pathways be directional and cull invalid pathways earlier at the reasoning step
            foreach (GameObject l in highlighted_lines) l.GetComponent<InstructionHighlights>().highlighted = false;
            highlighted_lines.Clear();
            
            List<string> flow_sources = new List<string>();
            if (switches[0].GetComponent<WaterSupply>().IsActivated()) flow_sources.Add("MAIN");
            if (vessels["B01"].volume > 1f) flow_sources.Add("B01");
            if (vessels["B02"].volume > 1f) flow_sources.Add("B02");
            if (vessels["B03"].volume > 1f) flow_sources.Add("B03");
            if (vessels["B04"].volume > 1f) flow_sources.Add("B04");
            
            int[] pathways_yet_unused = new int[pathways.Count];
            for (int i = 0;  i < pathways.Count; i++) pathways_yet_unused[i] = i;
            while (true) {
                bool addition_made = false;
                for (int i = 0; i < pathways.Count; i++) {
                    int pidx = pathways_yet_unused[i];
                    if (pidx == -1) continue;
                    string[] p = pathways[pidx];
                    string path_start = p[0];
                    string path_end   = p[p.Length-1];
                    foreach (string source in flow_sources) {
                        if (source == path_start || source == path_end) {
                            for (int nidx = 1; nidx < p.Length - 1; nidx++) {
                                string node_name = p[nidx];
                                try {
                                    if (!configuration_valves[node_name]) goto Pathway_Blocked;
                                }
                                catch {
                                try {
                                    if (proportional_valves[node_name] == 0.0f) goto Pathway_Blocked;
                                }
                                catch {
                                try {
                                    if (proportional_control_valves[node_name].operating_point_manual == 0.0f) goto Pathway_Blocked;
                                }
                                catch {}}}
                            }
                            if (!flow_sources.Contains(path_start)) flow_sources.Add(path_start);
                            if (!flow_sources.Contains(path_end))   flow_sources.Add(path_end);
                            pathways_yet_unused[i] = -1;
                            for (int nidx = 1; nidx < p.Length-1; nidx++) {
                                string node_name = p[nidx];
                                if (node_name.StartsWith("Line")) {
                                    GameObject gobj = GameObject.Find(node_name);
                                    if (gobj != null) {
                                        if (!highlighted_lines.Contains(gobj)) {
                                            highlighted_lines.Add(gobj);
                                        }
                                    }
                                }
                            }
                            addition_made = true;
                            Pathway_Blocked: int x;
                            break;
                        }
                    }
                }
                if (!addition_made) break;
            }
            
        }
        // Applying highlights
        foreach (GameObject l in highlighted_lines) l.GetComponent<InstructionHighlights>().highlighted = highlight_flow_lines;
    }
}
