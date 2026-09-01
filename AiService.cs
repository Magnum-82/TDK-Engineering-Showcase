using AEye.API.Models;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OpenAI.Chat;
using System.ClientModel;
using System.Data;

namespace AEye.API.Services
{
 
        public class AiService : IAiService
        {
            private readonly AppDbContext _context;
            private readonly AzureOpenAIClient _openAiClient; // Új kliens osztály a v2-ben
            private readonly string _deploymentName;
        private readonly string _readOnlyConnectionString;
        private readonly string _dbSchema = @"
You are an elite T-SQL expert for a corporate fleet management system.
STRICT RULE: Output ONLY the raw SQL code.
NO explanations, NO conversational text, NO markdown formatting (do not use ```sql).
If you cannot answer the question based on the schema, output exactly: SELECT 'Error' AS Result

IMPORTANT DOMAIN NOTES:
- Vehicles.SVillogo indicates whether the vehicle has a yellow rotating beacon (""sárga villogó"").
- InspectionResults.InspectionId references Inspections.Id.
- InspectionResults.Status is a logical flag: 1 = OK, 0 = Hiba.
- Inspections.LicensePlate = the license plate recorded during the inspection.
- Vehicles.Rendszam = the official license plate stored for the vehicle.

Database Schema:
- [Inspections] (Id, LicensePlate, Drivername, Username, Datum, ODO, Comment)
- [Vehicles] (VehicleId, Rendszam, Sarokszam, Gyartmany, Tipus, Felepitmeny, Kategoria, MaxTomeg, SVillogo, Evjarat, Cm3, Munkaszam, Alvazszam, GPS, RetiredDate, IsActive)
- [InspectionResults] (Id, InspectionId, TaskName, Status)

";

        public AiService(AppDbContext context, IConfiguration configuration, NL2SQLConfig nlConfig)
        {
            _context = context;

            // Azure adatok maradnak az IConfiguration-ből
            string endpoint = configuration["AzureOpenAI:Endpoint"] ?? "";
            string apiKey = configuration["AzureOpenAI:ApiKey"] ?? "";
            _deploymentName = configuration["AzureOpenAI:DeploymentName"] ?? "fleet-sql-model";

            // ITT HASZNÁLJUK AZ ÚJ CONFIG OSZTÁLYT:
            _readOnlyConnectionString = nlConfig.ConnectionString
                ?? throw new ArgumentNullException("Az NL2SQL Connection String üres!");

            _openAiClient = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey));
        }

        public async Task<object> GenerateAndExecuteSqlAsync(string prompt)
        {
            string generatedSql = string.Empty;

            // --- 1. AI GENERÁLÁSI FÁZIS ---
            try
            {
                // A specifikus modell (deployment) chat kliensének lekérése
                ChatClient chatClient = _openAiClient.GetChatClient(_deploymentName);

                // Opciók beállítása (v2 ChatCompletionOptions)
                var chatOptions = new ChatCompletionOptions()
                {
                    Temperature = 0.0f, // Tűpontos, determinisztikus válaszokhoz
                    MaxOutputTokenCount = 500
                };

                // Üzenetek összeállítása a rendszerszintű sémával
                var messages = new List<ChatMessage>
        {
            new SystemChatMessage(_dbSchema),
            new UserChatMessage($"User Question: {prompt}\nSQL:")
        };

                // Hívás az Azure felé
                ClientResult<ChatCompletion> response = await chatClient.CompleteChatAsync(messages, chatOptions);

                // SQL kinyerése és tisztítása (Markdown és felesleges karakterek eltávolítása)
                generatedSql = response.Value.Content[0].Text;
                generatedSql = generatedSql.Replace("```sql", "").Replace("```", "").Trim().TrimEnd(';');

                Console.WriteLine($"[Azure OpenAI] GENERÁLT SQL: {generatedSql}");

                // --- 2. BIZTONSÁGI SZŰRŐ (Application Level) ---
                if (!generatedSql.ToUpper().StartsWith("SELECT"))
                {
                    return new
                    {
                        Error = "Biztonsági szabálysértés",
                        Message = "Az AI csak SELECT (olvasási) műveleteket végezhet!",
                        SQL = generatedSql
                    };
                }
            }
            catch (Exception ex)
            {
                return new { Error = "AI Generálási hiba", Details = ex.Message };
            }

            // --- 3. ADATBÁZIS FUTTATÁSI FÁZIS (ReadOnly Connection) ---
            try
            {
                // Fontos: Nem a DbContext-et használjuk, hanem a korlátozott ReadOnly usert!
                // Ez a második védelmi vonal (Database Level Security)
                using (var connection = new SqlConnection(_readOnlyConnectionString))
                {
                    if (connection.State != ConnectionState.Open)
                        await connection.OpenAsync();

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = generatedSql;

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            var results = new List<Dictionary<string, object>>();

                            while (await reader.ReadAsync())
                            {
                                var row = new Dictionary<string, object>();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    row[reader.GetName(i)] = reader.GetValue(i);
                                }
                                results.Add(row);
                            }

                            // Visszatérés az adatokkal és a futtatott SQL-lel (ellenőrizhetőség miatt)
                            return new
                            {
                                Data = results,
                                Sql = generatedSql,
                                SecurityContext = "ReadOnly-User Enforcement"
                            };
                        }
                    }
                }
            }
            catch (SqlException sqlEx) when (sqlEx.Number == 229 || sqlEx.Number == 262)
            {
                // Speciális SQL hiba: nincs jogosultság (pl. UPDATE-et próbált az AI)
                return new
                {
                    Error = "Hozzáférés megtagadva",
                    Details = "Az adatbázis-szerver blokkolta a műveletet (ReadOnly korlátozás).",
                    SQL = generatedSql
                };
            }
            catch (Exception dbEx)
            {
                return new { Error = "Adatbázis futtatási hiba", Details = dbEx.Message, SQL = generatedSql };
            }
        }
    }
    } 
