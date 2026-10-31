using System.Text.Json;
using System.Text.Json.Serialization;
using ClaudePet.Util;

namespace ClaudePet.Core;

/// <summary>Réglages mémorisés dans %AppData%\Pet\config.json.</summary>
internal sealed class ConfigData
{
    public bool Calm { get; set; }
    public bool Muted { get; set; }
    public int Volume { get; set; } = 70;
    /// <summary>Taille de Pet : pixels écran par pixel art, à 100 % d'échelle Windows (3 à 14).</summary>
    public int Size { get; set; } = 7;
    /// <summary>Temps « PC allumé » depuis le dernier repas, en secondes (la jauge de faim).</summary>
    public double HungerSeconds { get; set; }
    /// <summary>Repère de temps allumé (sans veille) à la dernière sauvegarde.</summary>
    public double LastUnbiased { get; set; }
    /// <summary>Repère du démarrage de Windows à la dernière sauvegarde.</summary>
    public long BootStamp { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(ConfigData))]
internal sealed partial class ConfigJson : JsonSerializerContext
{
}

internal sealed class Config
{
    public const int MinSize = 3, MaxSize = 14, DefaultSize = 7;
    public static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Pet");
    public static readonly string FilePath = Path.Combine(Folder, "config.json");

    public ConfigData D = new();
    private string lastSaved = "";

    public bool Calm { get => D.Calm; set { D.Calm = value; Save(); } }
    public bool Muted { get => D.Muted; set { D.Muted = value; Save(); } }
    public int Volume { get => D.Volume; set { D.Volume = Math.Clamp(value, 0, 100); Save(); } }
    public int Size { get => D.Size; set { D.Size = Math.Clamp(value, MinSize, MaxSize); Save(); } }

    public void Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                string text = File.ReadAllText(FilePath);
                var d = JsonSerializer.Deserialize(text, ConfigJson.Default.ConfigData);
                if (d != null) D = d;
                lastSaved = text;
            }
        }
        catch (Exception e)
        {
            Log.Write("Config illisible, valeurs par défaut : " + e.Message);
            D = new ConfigData();
        }
        D.Volume = Math.Clamp(D.Volume, 0, 100);
        D.Size = Math.Clamp(D.Size == 0 ? DefaultSize : D.Size, MinSize, MaxSize);
        if (double.IsNaN(D.HungerSeconds) || D.HungerSeconds < 0) D.HungerSeconds = 0;
    }

    public void Save()
    {
        try
        {
            string text = JsonSerializer.Serialize(D, ConfigJson.Default.ConfigData);
            if (text == lastSaved) return;
            Directory.CreateDirectory(Folder);
            string tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, text);
            File.Move(tmp, FilePath, overwrite: true);
            lastSaved = text;
        }
        catch (Exception e)
        {
            Log.Write("Sauvegarde impossible : " + e.Message);
        }
    }
}
