# Platform-Independent Asynchronous Telemetry Validator 📱⚙️

A robust workaround for bypassing OS-level sandbox limitations in cross-platform mobile development (.NET MAUI). This module ensures that photos taken via the native device camera are sharp and correctly oriented by retroactively analyzing hardware accelerometer data.

## 🎯 The Engineering Challenge

When using high-level cross-platform APIs like `.NET MAUI MediaPicker`, the framework hands over control to the OS's native camera app (the "Sandbox"). During this time, the parent application loses direct control over the hardware, making it impossible to actively block a user from taking a blurry or tilted photo based on real-time sensor data.

## 💡 The "Blackbox" Solution

Instead of fighting the OS sandbox, this module implements an **Asynchronous Telemetry-Synchronization** architecture, acting like an airplane's black box:

1. **Background Logging:** Before the camera opens, a background task spins up, logging Z-axis and magnitude (G-force) data from the accelerometer to a local SQLite database at 5 FPS.
2. **Event Capture:** The user takes the photo in the native camera app.
3. **Retrospective Synchronization:** Once control returns to the app, the system reads the exact `LastWriteTime` of the saved JPEG file.
4. **Time-Window Validation:** It queries the SQLite black box for a specific 2.2-second window surrounding the file creation timestamp.
5. **Peak Detection:** If the telemetry data within that tight window exceeds the configured G-force or tilt thresholds, the photo is automatically rejected.

## 🔄 Architecture Flow

```text
[Start UI] -> [Start SQLite Blackbox Task] -> [Open OS Camera Sandbox] 
                                                    |
[App Paused] <--------------------------------------|
                                                    |
[Photo Saved] -> [Get JPEG LastWriteTime] -> [Query Blackbox (-2.2s window)]
                                                    |
[Validate Z-axis & Magnitude limits] <--------------|
                      |
             [Accept or Reject Photo]
