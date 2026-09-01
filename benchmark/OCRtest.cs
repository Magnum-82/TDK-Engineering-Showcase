using Azure;
using Azure.AI.Vision.ImageAnalysis;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Text;

// --- 1. REFERENCIA LISTA (A flottád) ---
var flotta = new HashSet<string> {
    "AECK245", "AECK246", "AECK247", "AECM311", "AEFK359", "AIAV065", "AOAD068",
    "AOAK950", "AOGM121", "AOJJ919", "AOJZ017", "AOJZ105", "HWD838", "IHY415",
    "ITL858", "IZS673", "JRS325", "JRS334", "JRS335", "KSS273", "KWL723",
    "KWL882", "KXZ897", "LFA146", "LJC676", "LJE194", "LTD372", "NJD620",
    "NJD621", "NJD622", "NJD623", "NJD624", "NJD625", "NJD626", "NJD627",
    "NKE255", "NKE258", "NKE261", "NKE262", "NKE264", "NKE265", "NKE266",
    "NKE269", "NKE288", "NKE289", "NKE571", "NKE572", "NKE573", "NKE574",
    "NKE575", "NNH891", "NNH892", "NNH893", "NOF851", "NOF852", "NOF853",
    "PUU594", "PXB045", "SBL234", "SKV255", "SVV082", "TES373", "XNZ011",
    "XOC128", "XPG691", "XPN329", "XTE442", "XTE443", "XWU650", "XYH589",
    "XYL626", "XYL627", "XYL628", "XZU937", "Targonca"
};

// --- 2. KONFIGURÁCIÓ ---
string endpoint = "https://aeyeocrdetect.cognitiveservices.azure.com/";
string apiKey = "***************************";
string imageFolder = @"D:\Egyetem\aeye\OCRtest";
string csvPath = Path.Combine(imageFolder, "benchmark_eredmenyek.csv");

var client = new ImageAnalysisClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
var report = new List<string>();
report.Add("Fajlnev;Idotartam_ms;Vegleges_Rendszam;Siker;Modszer;Nyers_Sorok");

string[] images = Directory.GetFiles(imageFolder, "*.jpg");
Console.WriteLine($">>> {images.Length} kép elemzése indul a flotta-lista alapján...");

// --- 3. FELDOLGOZÁS ---
foreach (var imagePath in images)
{
    var sw = Stopwatch.StartNew();
    var (plate, rawText, method) = await ProcessLicensePlateWithWhitelist(imagePath, client, flotta);
    sw.Stop();

    string fileName = Path.GetFileName(imagePath);
    bool success = !string.IsNullOrEmpty(plate);

    report.Add($"{fileName};{sw.ElapsedMilliseconds};{plate};{success};{method};{rawText.Replace(";", ",")}");
    Console.WriteLine($"[{(success ? "OK" : "!!")}] {fileName} -> {plate} ({method})");
}

File.WriteAllLines(csvPath, report, Encoding.UTF8);
Console.WriteLine($"\n>>> KÉSZ! CSV mentve: {csvPath}");

// --- SEGÉDMETÓDUSOK ---

async Task<(string plate, string rawText, string method)> ProcessLicensePlateWithWhitelist(string path, ImageAnalysisClient visionClient, HashSet<string> whitelist)
{
    var lines = await RunOcrAsync(path, visionClient);
    string allText = string.Join(" | ", lines);

    foreach (var sor in lines)
    {
        // Tisztítás: ALI HY-415 -> ALIHY415
        string tiszta = Regex.Replace(sor.Replace(".", "").Replace("-", "").Replace(" ", "").ToUpper(), @"[^A-Z0-9]", "");
        if (string.IsNullOrEmpty(tiszta)) continue;

        // 1. LÉPÉS: Tartalmazza-e pontosan? (Substring match)
        // Ha a tiszta sorban (ALIHY415) benne van bármelyik flotta-elem (IHY415)
        foreach (var vart in whitelist)
        {
            if (tiszta.Contains(vart))
            {
                return (vart, allText, "Flotta-Tartalmazza (Pontos)");
            }
        }

        // 2. LÉPÉS: "Sliding Window" Levenshtein (Fuzzy Substring)
        // Ha pl. HATL858-at kaptunk, megnézzük a 6-7 karakteres darabjait
        foreach (var vart in whitelist)
        {
            // Ha a felismert szöveg hosszabb, mint a várt rendszám
            if (tiszta.Length >= vart.Length)
            {
                // Végigmegyünk a tiszta szövegen "ablakokkal"
                for (int i = 0; i <= tiszta.Length - vart.Length; i++)
                {
                    string ablak = tiszta.Substring(i, vart.Length);
                    if (LevensteinTavolsag(ablak, vart) <= 1)
                    {
                        return (vart, allText, $"Fuzzy-Ablak ({ablak}->{vart})");
                    }
                }
            }
            else // Ha rövidebb, akkor sima Levenshtein
            {
                if (LevensteinTavolsag(tiszta, vart) <= 1)
                    return (vart, allText, "Fuzzy-Sima");
            }
        }
    }
    return ("", allText, "Nincs talalat");
}

async Task<List<string>> RunOcrAsync(string imagePath, ImageAnalysisClient visionClient)
{
    try
    {
        using var fileStream = File.OpenRead(imagePath);
        var result = await visionClient.AnalyzeAsync(BinaryData.FromStream(fileStream), VisualFeatures.Read);
        return result.Value.Read?.Blocks.SelectMany(b => b.Lines).Select(l => l.Text).ToList() ?? new List<string>();
    }
    catch { return new List<string>(); }
}

int LevensteinTavolsag(string s, string t)
{
    int n = s.Length, m = t.Length;
    int[,] d = new int[n + 1, m + 1];
    if (n == 0) return m;
    if (m == 0) return n;
    for (int i = 0; i <= n; d[i, 0] = i++) ;
    for (int j = 0; j <= m; d[0, j] = j++) ;
    for (int i = 1; i <= n; i++)
        for (int j = 1; j <= m; j++)
            d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (t[j - 1] == s[i - 1] ? 0 : 1));
    return d[n, m];
}