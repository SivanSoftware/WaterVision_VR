# WaterVision VR 🌊🥽

> **A virtual-reality platform for professional meetings in the water sector** - real-time transcription, 3D models, and remote collaboration.

Final project · Software Engineering · 2026

---

## 📖 Description

**WaterVision VR** is a comprehensive virtual-reality collaboration platform built for professionals in the water industry. Multiple participants join a shared virtual meeting room where they can talk in real time, inspect 3D models of water infrastructure, run simulations, track engagement, and document the session — all inside an immersive VR/AR environment.

The platform was **field-tested at the Beit She'an wastewater treatment plant (WWTP)** with industry professionals, featuring realistic simulations of water infrastructure and treatment processes.

---

## ✨ Features

| # | Module | Description |
|---|--------|-------------|
| 01 | **Full VR Meeting Room** | A 3D environment with synchronized avatars, hand tracking, and voice communication. Join a room in 3 seconds. |
| 02 | **Real-Time Transcription** | Speech-to-Text — everything said is transcribed per speaker, with PDF export at the end of the meeting. |
| 03 | **3D Model Room** | Import and display GLB/FBX models in VR, build pipelines, and run a live wastewater-treatment simulation. |
| 04 | **People Analytics** | Real-time tracking of participant engagement — speaking time and participation level. |
| 05 | **Sign Language & Gestures** | Hand-gesture recognition for alternative communication, precise hand sync, and a collaborative whiteboard. |
| 06 | **Augmented Reality (AR)** | Switch between full VR and Mixed Reality (Passthrough) on Meta Quest. |

---

## 🛠️ Tech Stack

| Area | Technology |
|------|------------|
| Engine | Unity **6000.3.10f1** (Unity 6.3) · C# |
| Rendering | Universal Render Pipeline (URP) |
| VR / XR | Meta XR SDK · OpenXR · XR Interaction Toolkit · XR Hands (Hand Tracking) |
| Platform | Meta Quest 3 |
| AR | AR Foundation · Android XR OpenXR (Passthrough) |
| Multiplayer | Netcode for GameObjects · Unity Multiplayer Services |
| Voice | Vivox |
| Speech-to-Text | Real-time transcription · Unity AI Inference (ONNX) |
| 3D Models | GLB / FBX (glTFast) |
| Streaming | Unity Render Streaming |

---

## 🗺️ Project Journey (Sprints)

- **Sprint 1 — Infrastructure & Core:** Basic VR environment, synchronized avatars, multiplayer communication, transcription foundation, and MVP definition.
- **Sprint 2 — Documentation, Sharing & Navigation:** Meeting recording, collaborative whiteboard, interactive tools, water-filtration simulation, and menus.
- **Sprint 3 — Advanced Features:** Augmented Reality (AR), WWTP simulation, model room & assembly, sign language — and a field visit to the Beit She'an plant.
- **Sprint 4 — Finalization:** Polishing, comprehensive testing, sign language, collaborative whiteboard, and the final presentation to the judges.

---

## 📁 Project Structure

```
WaterVision_VR_Project/
├── Assets/
│   ├── Scripts/            # Main project source code (C#)
│   ├── Netanel Scripts/    # Recording, transcription (STT), People Analytics, Whiteboard
│   ├── Scenes/             # Project scenes
│   ├── Prefabs/            # Avatars, panels, ready-made objects
│   ├── UI/                 # User interface
│   ├── Materials/          # Materials & shaders
│   └── ...
├── Packages/               # Package configuration (manifest.json)
└── ProjectSettings/        # Project settings
```

---

## 🚀 Getting Started

1. Install **Unity Hub** and Unity version **6000.3.10f1**.
2. Clone the repository:
   ```bash
   git clone https://github.com/SivanSoftware/WaterVision_VR.git
   ```
3. Open the folder through Unity Hub (`Add project from disk`).
4. On first open, Unity will import the assets and resolve the packages listed in `Packages/manifest.json` — this may take a few minutes.

---

## 📦 What's in this repository

✅ **The complete source code is here** — all C# scripts, scenes, prefabs, UI, materials, and project settings that make up WaterVision VR are version-controlled in this repository. The project's full code base stands on its own in Git.

⚠️ **Only the heavy third-party assets are excluded**, because they exceed Git/GitHub's size limits (100MB per file) and are not part of our own code. They are not committed and must be imported separately:

- **Asset Store / SDK packages:** Oculus / Meta XR, Mixamo, Synty Studios, NamuFX (Water Effects), Phoenix3D, Ready Player Me, Starter Assets.
- **AI models:** Speech-to-Text model files (`.onnx` / `.bin`) — place them back under `Assets/StreamingAssets/Whisper/` and `Assets/Netanel Scripts/STT/`.
- **Archives** (`.zip`) and other large binaries.

> Packages are managed through the Unity Package Manager / Asset Store and are restored when the project is opened.

---

## 👥 Team

Final Project 2026 · WaterVision VR

**Sivan Lasri · Elinor Srur · Netanel Shen · Omer Peleg · Shalev Turgeman**
