// Generated with Cursor (GPT-5.2) by OpenAI assistant on 2026-02-15.
using System;
using System.IO;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ProfileService : MonoBehaviour
{
    private const int slotCount = 3;
    private const int currentVersion = 2;

    private const string activeSlotKey = "ActiveProfileSlot_v1";
    private const string directoryName = "profiles";
    private const string filePrefix = "profile_slot_";
    private const string fileExt = ".json";

    public static ProfileService Instance { get; private set; }

    public static event Action<ProfileSlotId> ActiveSlotChanged;
    public static event Action<ProfileSlotId> ProfileChanged;

    [Header("Runtime (debug)")]
    [SerializeField] private ProfileSlotId activeSlot = ProfileSlotId.Slot1;

    [SerializeField] private ProfileSaveData[] slotProfiles = new ProfileSaveData[slotCount];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        var go = new GameObject(nameof(ProfileService));
        DontDestroyOnLoad(go);
        go.AddComponent<ProfileService>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadAllSlots();
        LoadActiveSlot();
    }

    public static ProfileSlotId GetActiveSlot()
    {
        if (Instance == null) return ProfileSlotId.Slot1;

        return Instance.activeSlot;
    }

    public static void SetActiveSlot(ProfileSlotId slot)
    {
        if (Instance == null) return;

        if (!IsValidSlot(slot))
        {
            Debug.LogWarning($"{nameof(ProfileService)}: Invalid slot '{slot}'.");
            return;
        }

        if (Instance.activeSlot == slot)
        {
            return;
        }

        Instance.activeSlot = slot;
        PlayerPrefs.SetInt(activeSlotKey, (int)slot);
        PlayerPrefs.Save();

        ActiveSlotChanged?.Invoke(slot);
    }

    public static ProfileSaveData GetActiveProfileCopy()
    {
        return GetProfileCopy(GetActiveSlot());
    }

    public static ProfileSaveData GetProfileCopy(ProfileSlotId slot)
    {
        if (Instance == null) return new ProfileSaveData();

        int index = (int)slot;
        if (index < 0 || index >= slotCount) return new ProfileSaveData();

        ProfileSaveData src = Instance.slotProfiles[index];
        if (src == null) return new ProfileSaveData();

        return DeepCopy(src);
    }

    public static void ResetSlot(ProfileSlotId slot)
    {
        if (Instance == null) return;

        int index = (int)slot;
        if (index < 0 || index >= slotCount) return;

        Instance.slotProfiles[index] = CreateNewProfile();
        Instance.SaveSlot(slot);
        ProfileChanged?.Invoke(slot);
    }

    public static void AddBankedPoints(double points)
    {
        if (Instance == null) return;
        if (points <= 0d) return;

        ProfileSaveData p = Instance.GetOrCreateActiveProfile();
        if (p.stats == null)
        {
            p.stats = new ProfileStats();
        }

        p.stats.AddPoints(points);
        Instance.SaveSlot(Instance.activeSlot);
        ProfileChanged?.Invoke(Instance.activeSlot);
    }

    public static void RecordRunCompleted()
    {
        if (Instance == null) return;

        ProfileSaveData p = Instance.GetOrCreateActiveProfile();
        if (p.stats == null)
        {
            p.stats = new ProfileStats();
        }

        p.stats.RecordRunCompleted();
        Instance.SaveSlot(Instance.activeSlot);
        ProfileChanged?.Invoke(Instance.activeSlot);
    }

    public static bool HasAnsweredFirstTimePlayingPrompt()
    {
        if (Instance == null) return false;

        ProfileSaveData p = Instance.GetOrCreateActiveProfile();
        return p != null && p.hasAnsweredFirstTimePlayingPrompt;
    }

    public static void RecordFirstTimePlayingPromptAnswer(bool isYes)
    {
        if (Instance == null) return;

        ProfileSaveData p = Instance.GetOrCreateActiveProfile();
        if (p == null) return;

        p.hasAnsweredFirstTimePlayingPrompt = true;
        p.isFirstTimePlayingAnswerYes = isYes;

        Instance.SaveSlot(Instance.activeSlot);
        ProfileChanged?.Invoke(Instance.activeSlot);
    }

    public static bool ConsumeCleanFirstRunSkipIfNeeded()
    {
        if (Instance == null) return false;

        ProfileSaveData p = Instance.GetOrCreateActiveProfile();
        if (p == null) return false;

        if (p.hasConsumedCleanFirstRunSkip)
        {
            return false;
        }

        if (!IsCleanProfile(p))
        {
            return false;
        }

        p.hasConsumedCleanFirstRunSkip = true;
        Instance.SaveSlot(Instance.activeSlot);
        ProfileChanged?.Invoke(Instance.activeSlot);
        return true;
    }

    private static bool IsCleanProfile(ProfileSaveData p)
    {
        if (p == null)
        {
            return true;
        }

        if (p.stats == null)
        {
            return true;
        }

        return p.stats.totalPointsScored <= 0d && p.stats.totalBoardWins <= 0;
    }

    private void LoadActiveSlot()
    {
        int raw = PlayerPrefs.GetInt(activeSlotKey, (int)ProfileSlotId.Slot1);
        var slot = (ProfileSlotId)Mathf.Clamp(raw, 0, slotCount - 1);
        activeSlot = slot;
    }

    private void LoadAllSlots()
    {
        EnsureStorageDirectoryExists();

        if (slotProfiles == null || slotProfiles.Length != slotCount)
        {
            slotProfiles = new ProfileSaveData[slotCount];
        }

        for (int i = 0; i < slotCount; i++)
        {
            var slot = (ProfileSlotId)i;
            slotProfiles[i] = LoadSlotOrNew(slot);
        }
    }

    private ProfileSaveData GetOrCreateActiveProfile()
    {
        int index = (int)activeSlot;
        if (index < 0 || index >= slotCount)
        {
            activeSlot = ProfileSlotId.Slot1;
            index = 0;
        }

        if (slotProfiles[index] == null)
        {
            slotProfiles[index] = CreateNewProfile();
        }

        if (slotProfiles[index].stats == null)
        {
            slotProfiles[index].stats = new ProfileStats();
        }

        return slotProfiles[index];
    }

    private ProfileSaveData LoadSlotOrNew(ProfileSlotId slot)
    {
        string path = GetSlotPath(slot);
        if (string.IsNullOrWhiteSpace(path))
        {
            return CreateNewProfile();
        }

        if (!File.Exists(path))
        {
            return CreateNewProfile();
        }

        try
        {
            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                return CreateNewProfile();
            }

            ProfileSaveData data = JsonUtility.FromJson<ProfileSaveData>(json);
            if (data == null)
            {
                return CreateNewProfile();
            }

            UpgradeInPlaceIfNeeded(data);
            if (data.stats == null)
            {
                data.stats = new ProfileStats();
            }

            return data;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"{nameof(ProfileService)}: Failed to load slot '{slot}'. {ex.Message}", this);
            return CreateNewProfile();
        }
    }

    private void SaveSlot(ProfileSlotId slot)
    {
        EnsureStorageDirectoryExists();

        int index = (int)slot;
        if (index < 0 || index >= slotCount) return;

        ProfileSaveData data = slotProfiles[index];
        if (data == null)
        {
            data = CreateNewProfile();
            slotProfiles[index] = data;
        }

        UpgradeInPlaceIfNeeded(data);

        try
        {
            string json = JsonUtility.ToJson(data);
            File.WriteAllText(GetSlotPath(slot), json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"{nameof(ProfileService)}: Failed to save slot '{slot}'. {ex.Message}", this);
        }
    }

    private static ProfileSaveData CreateNewProfile()
    {
        return new ProfileSaveData
        {
            version = currentVersion,
            displayName = "",
            stats = new ProfileStats()
        };
    }

    private static void UpgradeInPlaceIfNeeded(ProfileSaveData data)
    {
        if (data == null)
        {
            return;
        }

        if (data.version <= 0)
        {
            data.version = currentVersion;
        }

        if (data.version > currentVersion)
        {
            // Future version; keep as-is.
            return;
        }

        // v1 is current. Future migrations go here.
        data.version = currentVersion;
    }

    private static bool IsValidSlot(ProfileSlotId slot)
    {
        int index = (int)slot;
        return index >= 0 && index < slotCount;
    }

    private static ProfileSaveData DeepCopy(ProfileSaveData src)
    {
        if (src == null) return new ProfileSaveData();

        // JsonUtility roundtrip produces a deep copy for this simple data.
        try
        {
            string json = JsonUtility.ToJson(src);
            ProfileSaveData copy = JsonUtility.FromJson<ProfileSaveData>(json);
            return copy ?? new ProfileSaveData();
        }
        catch
        {
            return new ProfileSaveData();
        }
    }

    private static void EnsureStorageDirectoryExists()
    {
        try
        {
            string dir = GetProfilesDirectory();
            if (string.IsNullOrWhiteSpace(dir))
            {
                return;
            }

            Directory.CreateDirectory(dir);
        }
        catch
        {
            // Ignore.
        }
    }

    private static string GetProfilesDirectory()
    {
        string root = Application.persistentDataPath;
        if (string.IsNullOrWhiteSpace(root))
        {
            return "";
        }

        return Path.Combine(root, directoryName);
    }

    private static string GetSlotPath(ProfileSlotId slot)
    {
        string dir = GetProfilesDirectory();
        if (string.IsNullOrWhiteSpace(dir))
        {
            return "";
        }

        int index = (int)slot;
        return Path.Combine(dir, filePrefix + index + fileExt);
    }
}

