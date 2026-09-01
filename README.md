# 🚙 Fleet Management Engineering Showcase (AEYE FOR FLEET)

Welcome to my engineering portfolio! This repository contains curated, highly technical Proof-of-Concept (PoC) modules from my award-winning Institutional Scientific Students' Association (TDK) project: **AI-based Condition Monitoring for Corporate Vehicle Fleets (AEYEFORFLEET)**.

As a Business Informatics student and an active Fleet Group Leader, my passion lies in bridging the gap between physical operations and modern software engineering. Instead of publishing a monolithic application, I have extracted the most challenging engineering problems I solved during the development of this system.

🎓 **Full Research Paper & Documentation:**
You can read the complete TDK thesis, detailed methodology, and architectural blueprints here: 
👉 **[Read the Research Paper (OneDrive) 🔗][https://onedrive.live.com/?redeem=aHR0cHM6Ly8xZHJ2Lm1zL2IvYy9jZmFjNTE4NzJlMjI0OGNkL0lRQl9qbnloRF9wNVFyRThtcm8waWlNaUFYUTFtRzgyOXdqRHRhOV9mUkFZNUdnP2U9eVcyTlBT&cid=CFAC51872E2248CD&id=CFAC51872E2248CD%21sa17c8e7ffa0f4279b13c9aba348a2322&parId=CFAC51872E2248CD%21sb5bda9fdaa964a85a4a7d2b8ea7a6252&o=OneUp]**

---

## 📂 Featured Modules

### 1. [Platform-Independent Telemetry Validator (Sandbox Bypass)](./Telemetry-Sandbox-Bypass)
* **The Challenge:** Losing direct hardware control when invoking the OS-native camera (MediaPicker Sandbox), resulting in blurry or tilted inspection photos.
* **The Solution:** Built an asynchronous "blackbox" architecture using SQLite and real-time accelerometer data to retroactively validate photo stability via time-window synchronization.
* **Keywords:** `.NET MAUI`, `Sensors`, `SQLite`, `SignalR`, `Asynchronous Processing`

### 2. [OCR Post-Processing Engine](./OCR-PostProcessing)
* **The Challenge:** Real-world Optical Character Recognition (OCR) failures on dirty, skewed, or partially covered license plates.
* **The Solution:** Designed a lightweight, edge-friendly error correction pipeline combining Regex sanitization and a sliding-window Levenshtein distance algorithm to accurately match noisy AI outputs against a known fleet database.
* **Keywords:** `C#`, `Algorithms`, `Regex`, `Levenshtein Distance`, `Benchmarking`

### 3. [Secure NL2SQL Assistant](./NL2SQL-FleetAssistant)
* **The Challenge:** Allowing non-technical managers to query the database using natural language without exposing the system to AI-hallucinated SQL injections.
* **The Solution:** Implemented a "Defense in Depth" architecture utilizing Azure OpenAI, dynamic schema injection, and strict Read-Only ADO.NET connections to execute AI-generated queries safely.
* **Keywords:** `ASP.NET Core`, `Azure OpenAI v2`, `Prompt Engineering`, `Security`, `ADO.NET`

---

## 🛠️ Tech Stack & Skills Demonstrated

* **Languages:** C#, T-SQL, Bash
* **Frameworks & Tools:** .NET MAUI, ASP.NET Core, SignalR, Entity Framework Core
* **AI & Cloud:** Azure AI Vision, Azure OpenAI, Semantic Kernel concepts
* **Software Engineering:** Clean Architecture principles, Defense in Depth, Fault Tolerance, Data-driven validation (xUnit, Benchmarking).

## 💡 Why This Matters?
These components were built to run in imperfect, real-world conditions. Whether it's throttling telemetry data for unstable mobile networks, correcting AI hallucinations with classic string matching, or securing database connections from LLMs, this repository reflects a pragmatic, robust approach to enterprise software development.
