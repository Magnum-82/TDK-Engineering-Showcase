using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Prediction;
using System.Text;

// --- KONFIGURÁCIÓ (Már a jó adatokkal) ---
string predictionKey = "252173b3722e4e6c99577f940fcc0e62";
string endpoint = "https://germanywestcentral.api.cognitive.microsoft.com"; 
Guid projectId = Guid.Parse("fa57bdf4-1565-406c-853d-acd197ccaa9c");
string publishedName = "DAMSCAN";

string testFolderPath = @"D:\AEYE_DATA\SEGREG\azuretest\damaged";
string csvPath = Path.Combine(testFolderPath, "validacios_eredmenyek_reszletes.csv");

// Kliens inicializálása
var predictionApi = new CustomVisionPredictionClient(
    new ApiKeyServiceClientCredentials(predictionKey))
{ Endpoint = endpoint };

var results = new StringBuilder();
results.AppendLine("Fajlnev;Elvart;Eredmeny;Serules_Prob;Ep_Prob;Minden_Talalat;Statusz");

Console.WriteLine("📊 Részletes tudományos validáció indítása...");

var files = Directory.GetFiles(testFolderPath, "*.png");
int successCount = 0;

foreach (var file in files)
{
    string fileName = Path.GetFileName(file);
    string expected = fileName.Contains("dmg") ? "Serules" : "Clean";

    try
    {
        using (var stream = File.OpenRead(file))
        {
          
            var result = await predictionApi.ClassifyImageAsync(projectId, publishedName, stream);

            // 1. Megkeressük a legmagasabb értékeket címkénként
            var sProb = result.Predictions.Where(p => p.TagName == "Serules").Max(p => (double?)p.Probability) ?? 0;
            var eProb = result.Predictions.Where(p => p.TagName == "Clean").Max(p => (double?)p.Probability) ?? 0;

            // 2. Nyers adatok összefűzése elemzéshez
            string allTags = string.Join("|", result.Predictions
                .OrderByDescending(p => p.Probability)
                .Select(p => $"{p.TagName}:{(p.Probability * 100):F1}%"));

            // 3. Küszöbérték alapú döntés (Threshold Analysis-hez kiváló)
            double threshold = 25.0;
            string aiDecision = (sProb * 100 > threshold) ? "Serules" : "Clean";

            bool isCorrect = (expected == aiDecision);
            if (isCorrect) successCount++;
            string status = isCorrect ? "OK" : "HIBA";

            results.AppendLine($"{fileName};{expected};{aiDecision};{(sProb * 100):F2}%;{(eProb * 100):F2}%;{allTags};{status}");
            Console.WriteLine($"[{status}] {fileName} -> S:{sProb * 100:F1}% | C:{eProb * 100:F1}%");
        }
    }
    catch (Exception ex)
    {
        results.AppendLine($"{fileName};{expected};ERROR;0;0;{ex.Message};ERROR");
        Console.WriteLine($"[!] Hiba: {fileName} -> {ex.Message}");
    }
}

File.WriteAllText(csvPath, results.ToString(), Encoding.UTF8);

double accuracy = (double)successCount / files.Length * 100;
Console.WriteLine($"\n✅ KÉSZ! Pontosság ({successCount}/{files.Length}): {accuracy:F2}%");
Console.WriteLine($"Részletes CSV mentve: {csvPath}");