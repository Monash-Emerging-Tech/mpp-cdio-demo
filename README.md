# MPP Operator Training Demo

A digital twin of Monash Pilot Processes' (MPP) Wastewater Treatment Pilot Plant in VR. Will be used to train students on safe operation of the plant.

[![Unity 2022.3.36f1](https://img.shields.io/badge/Unity-2022.3.36f1-blue)](https://unity.com/)

---

## Table of Contents

- [Description](#description)
- [Features](#features)
- [Getting Started](#getting-started)
  - [Dependencies](#dependencies)
  - [Installing](#installing)
  - [Executing programnning](#executing--programnning)
- [Project Structure](#project-structure)
- [Help](#help)
- [Version History](#version-history)

---

## Description

MPP Operator Training Demo is a Unity-powered VR experience that simulates the operation of a Wastewater Treatment Pilot Plant. Users are guided through an example procedure of operation, and can visualise plant measurements and statistics.
This build is being developed for use in training with students.

---

## Features

- **Detailed Model**
  - 1:1 twin of the plant modelled in 3D
- **Interactive Simulation**
  - Users can interact with plant components (valves, pumps, etc.)
- **Guided Operation**
  - Users are instructed on operational procedure
- **VR Compatible**
  - Interact with components using VR controllers or mouse and keyboard.

---

## Getting Started

### Dependencies

- **Unity Editor**: 2022.3.36f1 (LTS)
- **XR & VR**
  - XR Interaction Toolkit v2.5.4
  - XR Management v4.5.1
  - OpenXR Plugin v1.11.0
- **Supported Headsets**
  - **Oculus Quest 2/3** via OpenXR 
  - **Other OpenXR-compatible** headsets

### Installing

1. **Clone the repo**

```bash
   git clone https://github.com/Monash-Emerging-Tech/mpp-cdio-demo
   cd mpp-cdio-demo
```

2. **Open in Unity Hub**

- Click Add, navigate to the cloned folder.
- Select Unity 2022.3.36f1 (or install via Hub if missing).

2. **Restore packages**

- Unity will detect the **Packages/manifest.json** file and auto-install everything.
- If you see any missing packages under **Window** > **Package Manager**, hit Install on the ones called out in Dependencies.

### Executing program

1. **Open Unity Project**

- In the Project window, navigate **Assets** > **Scenes** and double click **SampleScene.unity**.

2. **Press Play in the Editor**

- Press ▶ Play in the Unity Editor.

3. **Put on your VR headset and grab the controllers.**

---

## Project Structure

```
mpp-cdio-demo/
├───Assets/
│   ├───Materials/
│   ├───MPP_Prefabs/   # Custom prefabs for plant interaction
│   ├───OutlineEffect/
│   ├───PlantMeshes/   # 3D objects for plant components 
│   ├───Plugins/
│   ├───Resources/
│   ├───Samples/
│   ├───Scenes/        # Unity scenes
│   ├───Sounds/
│   ├───TextMesh Pro/
│   ├───XR/
│   └───XRI/
├───Library/
├───Packages/
├───ProjectSettings/
├───UserSettings/
├───.gitignore
├───mpp-cdio-demo.sln
├───Assembly-CSSharp.csproj
├───Unity.XR.Interaction.Toolkit.Samples.StarterAssets.csproj
├───UpgradeLog.htm
└───README.md          # ← you are here
```

---

## Help

- **Missing packages:** Open **Window** > **Package Manager**, search for XR Interaction Toolkit and click Install.

---

## Version History

v1.0.0

---
