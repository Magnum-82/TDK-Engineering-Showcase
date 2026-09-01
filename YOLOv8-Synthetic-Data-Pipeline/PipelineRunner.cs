using AEye.API.Services;
using System.Diagnostics;
using OpenCvSharp;

// --- ÚTVONALAK  ---
string modelPath = @"models/yolov8n-seg.onnx";
string inputDir = @"data/clean_images";
string outputDir = @"data/isolated_output";
string damagedDir = @"data/synthetic_damaged";
string texturesDir = @"data/damage_textures";

// Mappák biztosítása
if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
if (!Directory.Exists(damagedDir)) Directory.CreateDirectory(damagedDir);
if (!Directory.Exists(texturesDir)) Directory.CreateDirectory(texturesDir);

Console.WriteLine("🚀 AEye TDK Pipeline Indítása...");
using var service = new VehicleSegmentationService(modelPath);

// --- 1. FÁZIS: IZOLÁCIÓ ÉS TISZTÍTÁS ---
var files = Directory.GetFiles(inputDir, "*.jpg").OrderBy(f => f).ToArray();
Console.WriteLine($"--- 1. Fázis: {files.Length} kép izolálása ---");

foreach (var file in files)
{
    Console.Write($"Feldolgozás: {Path.GetFileName(file)}... ");
    var sw = Stopwatch.StartNew();

    // Itt hívjuk a szervizt, megadva az output könyvtárat
    var resultPath = await service.IsolateVehicleAsync(file, outputDir);
    sw.Stop();

    if (resultPath != null) Console.WriteLine($"✅ ({sw.ElapsedMilliseconds}ms)");
    else Console.WriteLine("❌ Kihagyva.");
}

// --- 2. FÁZIS: SZINTETIKUS SÉRÜLÉS GENERÁLÁS ---
var isolatedFiles = Directory.GetFiles(outputDir, "*_isolated.png");
Console.WriteLine($"\n--- 2. Fázis: {isolatedFiles.Length} képből sérülések gyártása ---");

if (Directory.GetFiles(texturesDir, "*.png").Length == 0)
{
    Console.WriteLine("⚠️ FIGYELEM: Nincs PNG a textures mappában! A 2. fázis kimarad.");
}
else
{
    foreach (var isolatedFile in isolatedFiles)
    {
        Console.Write($"Pusztítás: {Path.GetFileName(isolatedFile)}... ");

        using Mat isolatedMat = Cv2.ImRead(isolatedFile, ImreadModes.Unchanged);

        // Csináljunk minden képből 2 különböző sérült variációt
        for (int i = 1; i <= 2; i++)
        {
            using Mat damagedMat = service.ApplyRandomDamage(isolatedMat, texturesDir);

            string fileName = Path.GetFileNameWithoutExtension(isolatedFile).Replace("_isolated", "");
            string outPath = Path.Combine(damagedDir, $"{fileName}_dmg_v{i}.png");

            Cv2.ImWrite(outPath, damagedMat);
        }
        Console.WriteLine("✅ KÉSZ (2 variáció)");
    }
}

Console.WriteLine("\n🎯 Minden folyamat lefutott! Mehet a pihenés.");
Console.WriteLine("Nyomj egy gombot a kilépéshez...");
Console.ReadKey();