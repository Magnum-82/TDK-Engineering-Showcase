using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Training;
using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Training.Models;

string trainingKey = "********************";
string endpoint = "https://germanywestcentral.api.cognitive.microsoft.com/";
string imageFolderPath = @"D:\Egyetem\aeye\SZEGMENTALAS";

// 1. Kliens létrehozása
CustomVisionTrainingClient trainingApi = new CustomVisionTrainingClient(
    new Microsoft.Azure.CognitiveServices.Vision.CustomVision.Training.ApiKeyServiceClientCredentials(trainingKey))
{
    Endpoint = endpoint
};

try
{
    Console.WriteLine(">>> Kapcsolódás az Azure Custom Vision-höz...");

    // 2. Projekt megkeresése vagy létrehozása
    var projects = await trainingApi.GetProjectsAsync();
    var project = projects.FirstOrDefault(p => p.Name == "AEYE_Damage_Research");

    if (project == null)
    {
        Console.WriteLine(">>> Projekt nem található, új létrehozása...");
        // A General (A1) domain ideális a tárgyak/textúrák felismeréséhez
        var domains = await trainingApi.GetDomainsAsync();
        var objDetectionDomain = domains.First(d => d.Type == "ObjectDetection" && d.Name == "General (A1)");
        project = await trainingApi.CreateProjectAsync("AEYE_Damage_Research", "Sérülés detekció szegmentált képeken", objDetectionDomain.Id);
    }

    // 3. Tag (Címke) előkészítése
    var tags = await trainingApi.GetTagsAsync(project.Id);
    var damageTag = tags.FirstOrDefault(t => t.Name == "Serules")
                    ?? await trainingApi.CreateTagAsync(project.Id, "Serules");

    // 4. Mappa ellenőrzése és fájlok beolvasása
    if (!Directory.Exists(imageFolderPath))
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[HIBA] A megadott mappa nem létezik: {imageFolderPath}");
        Console.ResetColor();
        return;
    }

    var images = Directory.GetFiles(imageFolderPath, "*.png");
    Console.WriteLine($">>> {images.Length} db kép feldolgozása kezdődik...");

    foreach (var imagePath in images)
    {
        string fileName = Path.GetFileName(imagePath);

        // Fájl meglétének ellenőrzése közvetlenül a feltöltés előtt (paranoid mód)
        if (!File.Exists(imagePath))
        {
            Console.WriteLine($"[!] Kihagyva: {fileName} (A fájl időközben eltűnt)");
            continue;
        }

        try
        {
            using (var stream = new MemoryStream(File.ReadAllBytes(imagePath)))
            {
                await trainingApi.CreateImagesFromDataAsync(project.Id, stream, new List<Guid> { damageTag.Id });
                Console.WriteLine($"[OK] {fileName} sikeresen feltöltve és címkézve.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HIBA] Hiba történt {fileName} feltöltésekor: {ex.Message}");
        }
    }

    // 5. Tanítás indítása
    Console.WriteLine("\n>>> Tanítás indítása a felhőben... Ez eltarthat egy ideig.");
    var iteration = await trainingApi.TrainProjectAsync(project.Id);

    while (iteration.Status == "Training")
    {
        Console.WriteLine($">>> Állapot: {iteration.Status}...");
        await Task.Delay(5000);
        iteration = await trainingApi.GetIterationAsync(project.Id, iteration.Id);
    }

    Console.WriteLine(">>> KÉSZ! A modell sikeresen betanult.");
}
catch (Exception globalEx)
{
    Console.WriteLine($"\nKritikus hiba a folyamat során: {globalEx.Message}");
}