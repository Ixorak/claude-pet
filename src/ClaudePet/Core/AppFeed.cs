using ClaudePet.Native;
using ClaudePet.Util;

namespace ClaudePet.Core;

// Nourrir et faim (étape 4).
//
// La faim compte le temps « PC allumé » depuis le dernier repas, avec QueryUnbiasedInterruptTime :
// ce compteur de Windows ne compte ni la veille ni l'hibernation, et il repart de zéro au démarrage.
// Le temps où le PC est éteint ne compte donc pas. Le temps où le PC est allumé sans que Pet tourne compte.
internal sealed unsafe partial class App
{
    /// <summary>Au bout de 3 h de PC allumé sans manger, la jauge est vide : Pet a faim.</summary>
    public const double HungryAfter = 3 * 3600;
    /// <summary>De 3 h à 8 h sans manger, il ralentit jusqu'à ne plus bouger.</summary>
    public const double StarvingAfter = 8 * 3600;
    private const double HungerCap = 12 * 3600;

    public Berry Berry = null!;
    private double lastUnbiased, nextHungerSave;

    private void InitFeed()
    {
        Berry = new Berry(this);
        InitHunger();
    }

    // ------------------------------------------------------------------ faim

    private void InitHunger()
    {
        var d = Cfg.D;
        double u = SysInfo.UnbiasedSeconds;
        long boot = SysInfo.BootStamp;
        if (d.BootStamp != 0)
        {
            bool sameBoot = Math.Abs(d.BootStamp - boot) <= 2 && u >= d.LastUnbiased;
            // même démarrage : tout le temps allumé depuis la dernière sauvegarde compte ;
            // redémarrage : seul le temps allumé depuis le démarrage compte (le PC éteint, non)
            double add = sameBoot ? u - d.LastUnbiased : u;
            d.HungerSeconds = Math.Min(HungerCap, d.HungerSeconds + Math.Max(0, add));
            Log.Write($"Faim : +{add / 60:F0} min ({(sameBoot ? "même démarrage" : "redémarrage")}), total {d.HungerSeconds / 3600:F2} h");
        }
        d.BootStamp = boot;
        d.LastUnbiased = u;
        lastUnbiased = u;
        nextHungerSave = Clock.Now + 60;
        Cfg.Save();
    }

    private void UpdateHunger(double now)
    {
        double u = SysInfo.UnbiasedSeconds;
        double delta = u - lastUnbiased;
        lastUnbiased = u;
        if (delta < 0 || delta > 6 * 3600) return; // horloge incohérente : on ignore ce pas
        var d = Cfg.D;
        bool wasHungry = Hungry;
        d.HungerSeconds = Math.Min(HungerCap, d.HungerSeconds + delta);
        if (!wasHungry && Hungry) Pet.OnBecameHungry();
        if (now >= nextHungerSave)
        {
            nextHungerSave = now + 60;
            StoreHunger();
        }
    }

    /// <summary>Recopie les repères de temps dans la config (sauvegardée ensuite).</summary>
    private void StoreHunger()
    {
        Cfg.D.LastUnbiased = lastUnbiased;
        Cfg.D.BootStamp = SysInfo.BootStamp;
        Cfg.Save();
    }

    /// <summary>Jauge de faim : 1 = rassasié, 0 = affamé.</summary>
    public double Satiety => MathX.Clamp(1 - Cfg.D.HungerSeconds / HungryAfter, 0, 1);

    public bool Hungry => Cfg.D.HungerSeconds >= HungryAfter;

    /// <summary>Facteur de vitesse dû à la faim : 1 sans faim, 0,7 quand elle arrive, puis jusqu'à 0.</summary>
    public double HungerSpeed
    {
        get
        {
            double h = Cfg.D.HungerSeconds;
            if (h < HungryAfter) return 1;
            return MathX.Clamp(0.7 * (1 - (h - HungryAfter) / (StarvingAfter - HungryAfter)), 0, 0.7);
        }
    }

    /// <summary>Pet mange une baie : la jauge remonte de moitié.</summary>
    public void FeedPet()
    {
        var d = Cfg.D;
        d.HungerSeconds = Math.Max(0, Math.Min(d.HungerSeconds, HungryAfter) - HungryAfter * 0.5);
        StoreHunger();
        Bubbles?.MarkDirty();
        Log.Write($"Miam : jauge à {Satiety * 100:F0} %");
    }

    // ------------------------------------------------------------------ nourrir

    public bool FeedingActive => Berry != null && Berry.Active;

    public void StartFeeding()
    {
        if (Pet.State is St.Hidden or St.HomeJump or St.EnterDoor or St.Dig or St.Underground) return;
        if (FeedingActive) return;
        Berry.Start();
        Pet.OnFeedingStarted();
        Wake();
    }

    /// <summary>Clic droit sur la baie : elle disparaît.</summary>
    public void CancelFeeding()
    {
        if (!FeedingActive) return;
        Berry.Poof();
        Pet.OnFeedingCancelled();
        Wake();
    }

    private partial bool HandleFeedWindow(nint hwnd, uint msg, nuint wp, nint lp, ref nint result)
        => Berry != null && Berry.HandleMessage(hwnd, msg, wp, lp, ref result);

    private partial void ShutdownFeed()
    {
        StoreHunger();
        Cfg.Flush(force: true);
        if (Berry != null)
        {
            Berry.Win.Hide();
            User32.DestroyWindow(Berry.Win.Hwnd);
        }
    }

    /// <summary>Commandes de test : « feed », « hunger 3.5 » (heures sans manger), « eat ».</summary>
    private bool FeedDebugCommand(string[] a)
    {
        switch (a[0])
        {
            case "feed":
                StartFeeding();
                return true;
            case "feedat":
                if (a.Length > 2 && !FeedingActive
                    && double.TryParse(a[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double fx)
                    && double.TryParse(a[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double fy))
                {
                    Berry.DropAt(fx, fy);
                    Pet.OnFeedingStarted();
                }
                return true;
            case "hunger":
                if (a.Length > 1 && double.TryParse(a[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double h))
                {
                    bool was = Hungry;
                    Cfg.D.HungerSeconds = MathX.Clamp(h * 3600, 0, HungerCap);
                    StoreHunger();
                    Bubbles?.MarkDirty();
                    if (!was && Hungry) Pet.OnBecameHungry();
                }
                Log.Write($"Faim : {Cfg.D.HungerSeconds / 3600:F2} h, jauge {Satiety * 100:F0} %, vitesse {HungerSpeed:F2}");
                return true;
            case "eat":
                FeedPet();
                return true;
        }
        return false;
    }
}
