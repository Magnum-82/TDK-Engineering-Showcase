# 🚙 YOLOv8 Segmentation & Synthetic Data Pipeline (C# / ONNX)

A pure C# Machine Learning and Computer Vision pipeline that executes YOLOv8 instance segmentation using `ONNX Runtime` and manipulates images via `OpenCvSharp`. This module was built to solve the "Data Scarcity" problem by procedurally generating synthetic training/testing data for a vehicle damage detection AI.

## 🎯 The Engineering Challenge

1. **Native C# Inference:** Running Python-based ML models in enterprise .NET environments often requires clunky API microservices. The goal was to run YOLOv8-seg natively on the edge using C#.
2. **Post-Processing Complexity:** Extracting pixel-perfect polygon masks from YOLOv8's raw tensor outputs (mask prototypes and coefficients) requires complex matrix multiplication and Sigmoid activation without the luxury of Python's `numpy`.
3. **Data Scarcity:** Real-world images of specific car body damages are hard to acquire. We needed a way to artificially augment perfectly intact cars with realistic, randomized damage textures to benchmark the damage-detection AI.

## 💡 The Pipeline Architecture

This module implements a two-stage computer vision pipeline:

### Stage 1: Vehicle Isolation (ONNX + OpenCV)
* Loads and normalizes the image into a `1x3x640x640` DenseTensor.
* Runs inference via Microsoft ONNX Runtime.
* Extracts bounding boxes and 32-dimensional mask coefficients.
* Performs matrix multiplication between mask prototypes and coefficients, applying a Sigmoid function to generate a binary mask.
* Uses OpenCV to find the largest contour (noise removal) and extracts the vehicle onto a transparent `BGRA` background.

### Stage 2: Procedural Damage Injection (Alpha Blending)
* Loads transparent PNG damage textures (scratches, dents).
* Randomly scales and positions the texture strictly within the boundaries of the extracted vehicle's alpha mask (ensuring damage doesn't float in the air).
* Applies pixel-by-pixel Alpha Blending `(car_pixel * (1-alpha)) + (damage_pixel * alpha)` to realistically merge the damage onto the car body.

## ⚠️ Memory Management Highlight
Working with unmanaged resources (OpenCV `Mat`) in a garbage-collected language (C#) easily leads to memory leaks. This code employs strict `using` statements and defensive `try-finally` blocks to explicitly dispose of split image channels and tensors.

## 🛠️ Tech Stack
* **C# / .NET**
* **Microsoft.ML.OnnxRuntime** (Direct Tensor manipulation)
* **OpenCvSharp4** (Matrix math, Thresholding, Contours, Alpha Blending)
* **YOLOv8-seg** (Exported to ONNX format)

---
*Note: For the best visual representation of this pipeline, imagine an intact car going in, being perfectly cut out from its background, and coming out with procedurally generated scratches seamlessly blended onto its doors and bumpers.*
