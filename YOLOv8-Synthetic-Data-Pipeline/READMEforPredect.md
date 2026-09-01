## ☁️ Automated Cloud Training Pipeline (Azure Custom Vision SDK)

To eliminate manual overhead, this module provides an automated C# training pipeline that bridges local synthetic data generation with cloud-based model training.

### Key Capabilities:
* **Programmatic Infrastructure Setup:** Automatically checks for the existence of the `"AEYE_Damage_Research"` project in Azure, creating it under the Object Detection (General A1) domain if missing.
* **Smart Tag Management:** Programmatically resolves or creates the `"Serules"` (Damage) tag ID.
* **Resilient Batch Uploader:** Iterates through local directories containing generated .png samples, implementing safety checks and error handling per file during cloud ingestion (`CreateImagesFromDataAsync`).
* **Asynchronous Training Orchestration:** Triggers cloud training (`TrainProjectAsync`) and runs a polling loop to monitor iteration status in real-time until the model is fully trained and ready for deployment.
