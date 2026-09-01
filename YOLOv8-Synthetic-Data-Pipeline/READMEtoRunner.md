# 🧪 Synthetic Data Generation & Pipeline Orchestrator

A console-based pipeline orchestrator designed to solve **Data Scarcity** in computer vision projects. By combining local YOLOv8 instance segmentation with procedural image blending, this tool automatically generates synthetic, damaged training samples from clean vehicle photographs.

## 🎯 The Purpose

Acquiring a large, balanced dataset of real-world damaged corporate vehicles is notoriously difficult. This pipeline acts as a **Data Augmentation Engine**, taking intact vehicle photos, isolating them from their backgrounds, and procedurally injecting realistic damage textures (scratches, dents) to feed into cloud training models.

## ⚙️ How the Pipeline Works

The execution flows through two distinct, automated phases:

### Phase 1: Vehicle Isolation (Inference & Background Removal)
* Scans the input directory for raw vehicle `.jpg` images.
* Utilizes the `VehicleSegmentationService` (powered by Microsoft ONNX Runtime and YOLOv8-seg) to generate a pixel-precise mask.
* Strips away the background, isolating the vehicle onto a transparent `BGRA` canvas, and measures precise inference execution times (`Stopwatch`).

### Phase 2: Procedural Damage Injection (Alpha Blending)
* Loads transparent PNG damage textures from a dedicated asset library.
* Iterates through the isolated vehicle assets.
* Randomly scales and positions damage textures strictly *within* the vehicle's alpha boundaries (ensuring realistic placement on the bodywork).
* Performs pixel-by-pixel alpha blending to seamlessly merge the damage and outputs **multiple randomized variations** (e.g., `_dmg_v1.png`, `_dmg_v2.png`) ready for AI model training.

## 📂 Project Structure & Configuration

Before running, ensure your local directory structure matches the orchestrator's expected configuration:

```text
📁 Root Directory/
│
├── yolov8n-seg.onnx          # YOLOv8 Segmentation model
├── Program.cs                # This pipeline orchestrator script
│
└── 📁 data/
    ├── clean_images/         # Raw input photos (*.jpg)
    ├── isolated_output/      # Phase 1 output: Background-removed cars
    ├── synthetic_damaged/    # Phase 2 output: Procedurally damaged variations
    └── damage_textures/      # Source transparent PNG scratches/dents
