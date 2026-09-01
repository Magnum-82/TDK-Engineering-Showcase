/* 
 * ==============================================================================
 * AEYE FOR FLEET - PROOF OF CONCEPT (TDK Project)
 * ==============================================================================
 * Note for Reviewers: 
 * This class represents the initial monolithic prototype used to validate the 
 * "Asynchronous Telemetry-Synchronization" concept bypassing the MediaPicker sandbox.
 * 
 * Future Architecture / Refactoring Roadmap:
 * - Migrate to full MVVM pattern.
 * - Extract Azure OCR & Levenshtein logic into a dedicated IOCRService.
 * - Extract SQLite Blackbox logging into an ITelemetryRepository.
 * ==============================================================================
 */

using Azure;
using Azure.AI.Vision.ImageAnalysis;
using Microsoft.Maui.Graphics.Platform;
using SQLite;
using System.Diagnostics;
using System.Text.RegularExpressions;
namespace AEYECAPTURE;

using Microsoft.AspNetCore.SignalR.Client;

public partial class InspPhoto : ContentPage
{
    // Mezők és folyamatvezérlők
    private int lepes = 0;
    private bool _cameraBusy = false;
    private bool _looksensor = true;

    // Szenzor adatok
    private double utolsoZ = 0;
    private double utolsoIntenzitas = 1.0;
    private double ming = 0.93;
    private double maxg = 1.07;


    // Fekete doboz (SQLite) vezérlők
    private SQLiteAsyncConnection _database;
    private CancellationTokenSource _blackboxTokenSource;

    public InspPhoto()
    {
        InitializeComponent();
        ResetAllBorders();
    }

    // ÉLETCIKLUS KEZELÉS
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // TAKARÍTÁS: Csak akkor, ha már volt korábban haladás (lepes > 0) VAGY ha maradtak szellemfotók a listában. Erre is teszt során jöttem rá hogy, kell. Ez a rész előttha elkezdtük a fotók készítését, majd visszemntünk a főmenűbe majd újra kezdtük a szemlét a korábbi fotók bennt maradtak, így lett 8-9 fotó is egy szemléhez a 6 helyett:D
        if (lepes > 0 || (PhotoStore.Photos != null && PhotoStore.Photos.Count > 0))
        {
            try
            {
                foreach (var photo in PhotoStore.Photos.ToList())
                {
                    if (File.Exists(photo.LocalPath))
                        File.Delete(photo.LocalPath);
                }
                PhotoStore.Photos.Clear();
                if (_database != null)
                {
                    await _database.DeleteAllAsync<SensorLogEntry>();
                    await _database.DeleteAllAsync<ValidationLog>();
                    Debug.WriteLine("--- SQLite Blackbox kipucolva (Zombi adatok törölve) ---");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Hiba a takarításkor: {ex.Message}");
            }
        }

        // Mindenképp 0-ról indulunk
        lepes = 0;

        // inicializálunk.Ennek MINDIG le kell futnia az 'if'-en kívül!
        await InitDatabase();

        await SetupSignalR();

        StartAccelerometer();

        // Kényszerített alaphelyzet, ne maradjanak zöld keretek
        ResetAllBorders(); 
        FolyamatKezelo();

        Debug.WriteLine("--- InspPhoto: Hard reset és DB inicializálva ---");
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // Gyorsulásmérő szenzor leállítása
        if (Accelerometer.Default.IsMonitoring)
            Accelerometer.Default.Stop();

        // NAPLÓZÁS LEÁLLÍTÁSA Nagyon fontos! Bár a fotózáskor leállítjuk a naplózást, de ha valamiért mégis maradt volna futó Task, akkor itt biztosan leállítjuk, hogy ne fusson feleslegesen a háttérben.

        _blackboxTokenSource?.Cancel();

        Debug.WriteLine("--- InspPhoto: Erőforrások leállítva. ---");
    }

    // adatbázis inicializálás és szenzor kezelés
    private async Task InitDatabase()
    {
        try
        {
            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "blackbox.db3");
            _database = new SQLiteAsyncConnection(dbPath);
            await _database.CreateTableAsync<SensorLogEntry>();
            await _database.CreateTableAsync<ValidationLog>();
        }
        catch (Exception ex) { Debug.WriteLine($"DB Hiba: {ex.Message}"); }
    }
    // Szenzor kezelés: Gyorsulásmérő indítása és eseménykezelő
    private void StartAccelerometer()
    {
        try
        {
            if (Accelerometer.Default.IsSupported)
            {
                Accelerometer.Default.ReadingChanged -= Accelerometer_ReadingChanged;
                Accelerometer.Default.ReadingChanged += Accelerometer_ReadingChanged;

                if (!Accelerometer.Default.IsMonitoring)
                    Accelerometer.Default.Start(SensorSpeed.UI);
            }
        }
        catch (Exception ex) { Debug.WriteLine($"Szenzor hiba: {ex.Message}"); }
    }
    // Szenzor eseménykezelő: Itt frissítjük a legutolsó Z értéket és az intenzitást, majd a UI-t is frissítjük. Ez a rész nagyon fontos, mert innen származnak azok az adatok, amik alapján eldöntjük, hogy a fotózás stabil volt-e vagy sem.
    private HubConnection hubConnection;

    private async Task SetupSignalR()
    {
        hubConnection = new HubConnectionBuilder()
            .WithUrl("https://aeyeforfleet.hu/szemleHub") // A te szervered URL-je
            .WithAutomaticReconnect() // Életmentő mobil környezetben!
            .Build();

        try
        {
            await hubConnection.StartAsync();
            Debug.WriteLine("SignalR: Kapcsolódva!");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SignalR hiba az indításkor: {ex.Message}");
        }
    }
    private DateTime _utolsoKuldés = DateTime.MinValue;
    private async void Accelerometer_ReadingChanged(object sender, AccelerometerChangedEventArgs e)
    {
        var data = e.Reading;
        utolsoZ = data.Acceleration.Z;
        utolsoIntenzitas = Math.Sqrt(data.Acceleration.X * data.Acceleration.X +
                                     data.Acceleration.Y * data.Acceleration.Y +
                                     data.Acceleration.Z * data.Acceleration.Z);

        // UI FRISSÍTÉS (Marad a főszálon)
        MainThread.BeginInvokeOnMainThread(() =>
        {
            lblProgress.Text = $"Z: {utolsoZ:F2} | G: {utolsoIntenzitas:F2} (Fotók: 6/{lepes})";

            if (Math.Abs(utolsoZ) > 0.35 || utolsoIntenzitas > 1.02)
            {
                BorderTiltStatus.BackgroundColor = Color.FromArgb("#C62828");
                LblTiltStatus.Text = "⚠️ TARTSON STABILAN / FERDE";
            }
            else
            {
                BorderTiltStatus.BackgroundColor = Color.FromArgb("#2E7D32");
                LblTiltStatus.Text = "✅ POZÍCIÓ MEGFELELŐ";
            }
        });

        // 3. SIGNALR TELEMETRIA KÜLDÉSE (Fojtással és hibakezeléssel)
        // Csak 200ms-onként küldünk (5 FPS)
        if (hubConnection != null &&
            hubConnection.State == HubConnectionState.Connected &&
            (DateTime.Now - _utolsoKuldés).TotalMilliseconds > 200)
        {
            _utolsoKuldés = DateTime.Now;

            try
            {
                await hubConnection.InvokeAsync("SendLiveTelemetry", new
                {
                    z = utolsoZ,
                    g = utolsoIntenzitas,
                    step = lepes
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SignalR hiba: {ex.Message}");
            }
        }
    }

    //A kiinduló probléma, az volt hogy a fotózás során előfordult, hogy a készülék remegett vagy ferde volt, ami miatt a fotók életlenek vagy használhatatlanok lettek.
    //Ezért szerettem volna egy olyan megoldást, ami valamilyen módon figyeli a szenzor adatokat a fotózás alatt és eldönti, hogy a körülmények megfelelőek-e a fotó elkészítéséhez.
    // Ez a saját ötletem amire kifejezetten büszke vagyok. Az első terv az volt, hogy majd a mediapicker CapturePhotoAsync() metódusa alatt figyelem a szenzor adatokat és nem engedem a fotó elkészítését ha, nem megfelelőek a szenzor körülmények.
    // Azonban az első tesztek alatt rájöttem, nincs hatásom erre a folyamatra mert ez a készülék op.rendszerének sajátja. 
    // Ezért jött az ötlet, hogy a fotózás megkezdésekor elindítok egy külön Task-ot ami folyamatosan naplózza a szenzor adatokat egy SQLite adatbázisba. Amikor a fotó elkészült, akkor leállítom ezt a Task-ot és lekérem az adott időintervallumban rögzített szenzor adatokat.
    // Ezek alapján döntöm el, hogy a fotózás stabil volt-e vagy sem. Jelenleg mondhatni memória pazarló, bár az adatok nem foglalnak sok helyet, a szenzor adatok vizsgálata miatt fut végig a naplózás a mediapicker alatt.
    // Már folyamatban van egy körkörös buffer megoldás kidolgozása, ami csak az utolsó 3-5mp adatot tárolja, így nem kell az egész időintervallumot végignézni a fotózás után, hanem csak a legutolsó 3-5mp adatot, ami sokkal hatékonyabb lesz.
    private async Task StartLoggingAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var entry = new SensorLogEntry
                {
                    Timestamp = DateTime.Now,
                    ZAxis = utolsoZ,
                    Magnitude = utolsoIntenzitas
                };

                if (_database != null) await _database.InsertAsync(entry);
                await Task.Delay(200, token);
            }
        }
        catch (TaskCanceledException) { }
        catch (Exception ex) { Debug.WriteLine($"Naplózási hiba: {ex.Message}"); }
    }

    // FOTÓZÁSI FOLYAMAT
    public async Task TakePhotoAsync()
    {
        if (_cameraBusy) return;

        bool kellEllenorzes = lepes < 4;
        // Elő szűrés. Ne is engedjük elindítani a kamerát, ha remeg. Jelenleg ezek a határértékek. Parkinsonosok ,idült alkoholisták hátrányban :D Bocsánat
        if (kellEllenorzes)
        {
            if (Math.Abs(utolsoZ) > 0.35 || utolsoIntenzitas > maxg || utolsoIntenzitas < ming)
            {
                await DisplayAlert("Rossz pozíció", "Kérlek, tartsd stabilan a telefont!", "OK");
                return;
            }
        }
        _cameraBusy = true;
        DateTime captureStartTime = DateTime.Now; // Pontos kezdési idő mentése

        _blackboxTokenSource = new CancellationTokenSource();
        var token = _blackboxTokenSource.Token;
        _ = Task.Run(() => StartLoggingAsync(token), token);

        try
        {
            var photo = await MediaPicker.Default.CapturePhotoAsync();
            _blackboxTokenSource.Cancel(); // Kamera bezárva, mérés leáll
            DateTime captureEndTime = DateTime.Now;

            if (photo != null)
            {
                string localPath = Path.Combine(FileSystem.AppDataDirectory, $"{lepes}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg");
                using (Stream sourceStream = await photo.OpenReadAsync())
                using (FileStream localStream = File.Create(localPath))
                {
                    await sourceStream.CopyToAsync(localStream);
                }

                // VALIDÁCIÓ (Most már a pontos időablakot nézzük)
                bool kellUtovizsgalat = lepes < 4;

                if (kellUtovizsgalat)
                {
                    bool isStable = await IsCaptureStableAsync(localPath, captureStartTime, captureEndTime);

                    if (!isStable)
                    {
                        if (File.Exists(localPath)) File.Delete(localPath);
                        return; // Megállítjuk a folyamatot, nem ugrik a következő fázisra
                    }
                }
                PhotoStore.Photos.Add(new PhotoStore.PhotoInfo { LocalPath = localPath, FileName = Path.GetFileName(localPath), Step = lepes });

                if (lepes == 0)
                {
                    await ProcessLicensePlate(localPath);
                }

                lepes++;
                FolyamatKezelo();
            }
        }
        catch (Exception ex) { await DisplayAlert("Hiba", ex.Message, "OK"); }
        finally { _cameraBusy = false; }
    }

    // OCR + REGEX + LEVENSHTEIN. Jeleneg az azure cognitive service 4.0 verziót használom. Ez amúgy már a kiegészítések nélkül sem nagyon hibázik.
    // Az előző verzió az új formátúmú rendszámoknál a 2 betű CÍMER 2 betüre behalt. A címerrel nem tudott mit kezdeni. Ki lehetett venni azt is, működött de ez egy megbízhatóbb modell.
    // A regex-el szűröm a felismerteket, hogy csak a magyar rendszám formátumok maradjanak meg.
    // Ez azért fontos, mert az OCR néha felismerhet olyan szövegeket is amik nem rendszámok, de hasonlítanak rájuk.
    // Sokáig szivatott egy 3x5 cm-es matrica az egyik autó szélvédőjének sarkában. Először én észre se vettem, de az AI mindenáron azt akarta leolvasni.
    // Ez a regex kifejezetten a magyar rendszámokra van szabva, így minimalizálja a téves felismeréseket.
    // A Levenhstein távolságot nem tudom van-e értelme használni.. Igazából a kutatásom során találtam rá én nem ismertem korábban és nagyon megtetszett :)
    //  Jelenleg ez nincs bekötve még gondolkozom rajta hogyan lenne értelme. Sok az egymás utáni rendszám nálunk. pl
    // NJD620,621,622,623,624,625,626. Ha hibázna az AI egy karaktert mondjuk a 625-626 nál akkor a Levenhstein távolság 1 lenne és automatikusan javítgatná a rendszámot a korábban elmentett érték alapján.
    // Na de melyikre??? Gondolkodtam hogy lehetne súlyozni akaraktereket melyek amelyeknél gyakori a tévesztés. Pl. B és 8, S és 5, I és 1.
    // Ha a felismerésben ezek a karakterek szerepelnek akkor lehetne egy kicsit engedékenyebb a javításnál.
    // Egyelőre ez inkább csak érdekességként van itt, majd ha még marad időm a TDK-ig, akkor jobban kidolgozom.
    // 1. Frissített Feldolgozó metódus
    private async Task ProcessLicensePlate(string path)
    {
        var felismertSorok = await RunOcrAsync(path);
        string plate = "";
        string modszer = "Nincs találat";

        // A bejelentkezéskor feltöltött flotta lista
        var flotta = Session.RendszamFromSql;

        // Összefűzzük a nyers sorokat naplózáshoz (később az ML.NET-hez kelleni fog)
        string rawOcrLog = string.Join(" | ", felismertSorok);

        foreach (var sor in felismertSorok)
        {
            // Tisztítás: ALI HY-415 -> ALIHY415
            string tiszta = Regex.Replace(sor.ToUpper(), @"[^A-Z0-9]", "");
            if (string.IsNullOrEmpty(tiszta)) continue;

            // A. LÉPÉS: Pontos egyezés (HashSet O(1) sebességgel)
            if (flotta.Contains(tiszta))
            {
                plate = tiszta;
                modszer = "Flotta-Pontos";
                break;
            }

            // B. LÉPÉS: Ablakozó Levenshtein (Sliding Window)
            // Ha a sor hosszabb vagy egyenlő, mint a várt rendszámok
            foreach (var vart in flotta)
            {
                if (tiszta.Length >= vart.Length)
                {
                    // Végigtoljuk az ablakot a zajos szövegen
                    for (int i = 0; i <= tiszta.Length - vart.Length; i++)
                    {
                        string ablak = tiszta.Substring(i, vart.Length);

                        // Szigorú távolság-ellenőrzés az ablakon belül
                        if (LevensteinTavolsag(ablak, vart) <= 1)
                        {
                            plate = vart;
                            modszer = $"Fuzzy-Ablak ({ablak}->{vart})";
                            break;
                        }
                    }
                }
                if (!string.IsNullOrEmpty(plate)) break;
            }

            if (!string.IsNullOrEmpty(plate)) break;
        }

        // 2. EREDMÉNY MENTÉSE ÉS VISSZAJELZÉS
        if (!string.IsNullOrEmpty(plate))
        {
            Session.DetectedPlateNumber = plate;

            // Itt frissítjük a korábbi ValidationLog bejegyzést az OCR adatokkal
            // Vagy beszúrunk egy újat a felismerés részleteivel
            await _database.InsertAsync(new ValidationLog
            {
                FileName = Path.GetFileName(path),
                Timestamp = DateTime.Now,
                OriginalOCR = rawOcrLog, // Ezt a mezőt add hozzá a ValidationLog osztályhoz!
                DetectedPlateNumber = plate, // Ezt is!
                Method = modszer, // Ezt is!
                IsAccepted = true
            });

            await DisplayAlert("Siker!", $"Rendszám: {plate}\n({modszer})", "OK");
        }
        else
        {
            // Ha elbukott, akkor is mentsük el, hogy mi volt a nyers OCR (ML.NET tanításhoz aranyat ér!)
            await _database.InsertAsync(new ValidationLog
            {
                FileName = Path.GetFileName(path),
                Timestamp = DateTime.Now,
                OriginalOCR = rawOcrLog,
                Method = "FAIL",
                IsAccepted = false
            });

            await DisplayAlert("OCR Hiba", "A leolvasott szöveg nem szerepel a flottalistában.", "OK");
        }
    }

    private int LevensteinTavolsag(string s, string t)
    {
        int n = s.Length, m = t.Length;
        int[,] d = new int[n + 1, m + 1];
        if (n == 0) return m;
        if (m == 0) return n;
        for (int i = 0; i <= n; d[i, 0] = i++) ;
        for (int j = 0; j <= m; d[0, j] = j++) ;
        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }
        return d[n, m];
    }

    private async Task<List<string>> RunOcrAsync(string imagePath)
    {
        // 1. MEGJELENÍTJÜK a látványos popup-ot
        loadingOverlay.IsVisible = true;

        try
        {
            // 2. SZIMULÁLT IDŐZÍTÉS ha kell
           // await Task.Delay(5000);

            // 3. VALÓDI AZURE HÍVÁS
            string endpoint = "https://aeyeocrdetect.cognitiveservices.azure.com/";
            string apiKey = "***************";

            var client = new ImageAnalysisClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
            using var fileStream = File.OpenRead(imagePath);
            var result = await client.AnalyzeAsync(BinaryData.FromStream(fileStream), VisualFeatures.Read);

            return result.Value.Read?.Blocks
                .SelectMany(b => b.Lines)
                .Select(l => l.Text)
                .ToList() ?? new List<string>();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Azure OCR Hiba: {ex.Message}");
            await DisplayAlert("Hiba", "Nem sikerült az OCR elemzés. Ellenőrizd a kapcsolatot!", "OK");
            return new List<string>();
        }
        finally
        {
            // 4. ELTÜNTETJÜK a popup-ot a végén
            loadingOverlay.IsVisible = false;
        }
    }





    // Itt történik a fotó validációja. A beállított paraméterek alapján a mentett szenor adatokat vizsgálja a fotózás időpontjának környezetében.
    // hogy mi a jó időablak még tesztelem, egész jó az iránysztem.
    private async Task<bool> IsCaptureStableAsync(string photoPath, DateTime start, DateTime end)
    {
        try
        {
            // Ez a fájl mentésének vége
            DateTime saveFinished = File.GetLastWriteTime(photoPath);
            DateTime photoTimestamp = saveFinished;
            // Mivel az írás/kódolás időt vesz igénybe, a fotó pillanata valahol 
            // 200-500ms-mal a mentés vége előtt volt.
            // Nézzünk egy 2 másodperces ablakot, ami biztosan lefedi az expozíciót:
            DateTime windowStart = photoTimestamp.AddSeconds(-2.2);
            DateTime windowEnd = photoTimestamp;

            // Csak ebből a szűk intervallumból kérjük le a logokat
            var logs = await _database.Table<SensorLogEntry>()
                .Where(l => l.Timestamp >= windowStart && l.Timestamp <= windowEnd)
                .ToListAsync();

            if (logs == null || !logs.Any()) return false;

            // Statisztikai elemzés a szűkített ablakon
            double maxG = logs.Max(l => l.Magnitude);
            double minG = logs.Min(l => l.Magnitude);
            double maxDoles = logs.Max(l => Math.Abs(l.ZAxis));

            bool stabil = (maxG <= maxg && minG >= ming && maxDoles <= 0.35);

            // Mentés a validációs naplóba (bizonyítéknak)
            await _database.InsertAsync(new ValidationLog
            {
                FileName = Path.GetFileName(photoPath),
                Timestamp = photoTimestamp,
                MaxMagnitude = maxG,
                MinMagnitude = minG,
                MaxTilt = maxDoles,
                IsAccepted = stabil
            });

            if (!stabil) await DisplayAlert("⚠️ SIKERTELEN", "A fotózás pillanatában bemozdult a telefon!", "Újra");

            return stabil;
        }
        catch { return true; }
    }

    private void OnTakePhotoTapped(object sender, EventArgs e)
    {
        if (sender is Border b && b.IsEnabled) _ = TakePhotoAsync();
    }

    // Erre lehet hoogy lenne szebb megoldás, de működik és tiszta. Nem tervezem új lépés felvételét,
    // Ha így alakulna kitalálnék mást.
    private void FolyamatKezelo() 

    {
        switch (lepes)
        {
            case 0: SetActiv(borderEleje); break;
            case 1: SetActiv(borderBoldala); SetDone(borderEleje); lblStatusEleje.Text = "Rögzítve"; break;
            case 2: SetActiv(borderHatulja); SetDone(borderBoldala); lblStatusBoldala.Text = "Rögzítve"; break;
            case 3: SetActiv(borderJoldala); SetDone(borderHatulja); lblStatusHatulja.Text = "Rögzítve"; break;
            case 4: SetActiv(borderBelseje); SetDone(borderJoldala); lblStatusJoldala.Text = "Rögzítve"; break;
            case 5: SetActiv(borderMufala); SetDone(borderBelseje); lblStatusBelseje.Text = "Rögzítve"; break;

            default: SetDone(borderMufala); lblStatusMufala.Text = "Rögzítve"; btnContinue.IsEnabled = true; btnContinue.BackgroundColor = Color.FromArgb("#FFD700"); btnContinue.Opacity = 1; DisplayAlert("Kész", "Minden fotó elkészült!", "OK"); break;
        }
    }

    private void ResetAllBorders() { SetInactiv(borderEleje); SetInactiv(borderBoldala); SetInactiv(borderJoldala); SetInactiv(borderHatulja); SetInactiv(borderMufala); SetInactiv(borderBelseje); }
    private void SetInactiv(Border b) { b.IsEnabled = false; b.Opacity = 0.5; b.BackgroundColor = Color.FromArgb("#444444"); }
    private void SetActiv(Border b) { b.IsEnabled = true; b.Opacity = 1; b.BackgroundColor = Color.FromArgb("#FFD700"); }
    private void SetDone(Border b) { b.IsEnabled = false; b.Opacity = 1; b.BackgroundColor = Color.FromArgb("#32CD32"); }

    private async void OnbtnContinueClicked(object sender, EventArgs e) => await Navigation.PushAsync(new IdentificationPage());
}




