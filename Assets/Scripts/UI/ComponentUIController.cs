using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class ComponentUIController : MonoBehaviour
{
    [SerializeField] private Vector3 newBoardPosition;
    [SerializeField] private Vector3 newBoardRotation;
    [SerializeField] private Vector3 newBoardScale;
    [SerializeField] private Vector3 originalBoardPosition;
    [SerializeField] private Vector3 originalBoardRotation;
    [SerializeField] private Vector3 originialBoardScale;
    [SerializeField] private BoardRoot boardRoot;
    [SerializeField] private BoardComponent chosenComponent;
    [SerializeField] private TextMeshProUGUI componentTypeText;

    private readonly Dictionary<int, string> displayNameByInstanceId = new Dictionary<int, string>();

    void Awake()
    {
        boardRoot = FindFirstObjectByType<BoardRoot>();
        chosenComponent = FindFirstObjectByType<BoardComponent>();
    }

    void OnEnable()
    {
        if (boardRoot == null)
        {
            boardRoot = FindFirstObjectByType<BoardRoot>();
        }
        if (boardRoot == null)
        {
            return;
        }
        
        if (chosenComponent == null)
        {
            chosenComponent = FindFirstObjectByType<BoardComponent>();
        }
        if (chosenComponent == null)
        {
            return;
        }

        originalBoardPosition = boardRoot.transform.position;
        originalBoardRotation = boardRoot.transform.rotation.eulerAngles;
        originialBoardScale = boardRoot.transform.localScale;
        boardRoot.transform.position = newBoardPosition;
        boardRoot.transform.rotation = Quaternion.Euler(newBoardRotation.x, newBoardRotation.y, newBoardRotation.z);
        boardRoot.transform.localScale = newBoardScale;

        var session = GameSession.Instance;
        if (session != null && boardRoot != null)
        {
            session.ApplyBoardComponentUpgradesForScene(boardRoot.gameObject.scene.name);
        }

        BuildDisplayNamesForCurrentBoardScene();
        chosenComponent.Select();
        Refresh();
    }

    void OnDisable()
    {
        if (boardRoot == null)
        {
            return;
        }

        boardRoot.transform.position = originalBoardPosition;
        boardRoot.transform.rotation = Quaternion.Euler(originalBoardRotation.x, originalBoardRotation.y, originalBoardRotation.z);
        boardRoot.transform.localScale = originialBoardScale;
        if (chosenComponent != null)
        {
            chosenComponent.DeSelect();
        }
    }

    void OnLeft()
    {
        chosenComponent.DeSelect();
        chosenComponent = chosenComponent.leftObject.GetComponent<BoardComponent>();
        Refresh();
    }

    void OnRight()
    {
        chosenComponent.DeSelect();
        chosenComponent = chosenComponent.rightObject.GetComponent<BoardComponent>();
        Refresh();
    }

    void OnUp()
    {
        chosenComponent.DeSelect();
        chosenComponent = chosenComponent.upObject.GetComponent<BoardComponent>();
        Refresh();
    }

    void OnDown()
    {
        chosenComponent.DeSelect();
        chosenComponent = chosenComponent.downObject.GetComponent<BoardComponent>();
        Refresh();
    }

    void Refresh()
    {
        chosenComponent.Select();
        if (componentTypeText != null)
        {
            componentTypeText.text = GetDisplayNameFor(chosenComponent);
        }
        UpgradeComponents[] upgradeComponents = GetComponentsInChildren<UpgradeComponents>();

        foreach (UpgradeComponents upgrade in upgradeComponents)
        {
            upgrade.Refresh(chosenComponent.gameObject);
        }
    }

    private void BuildDisplayNamesForCurrentBoardScene()
    {
        displayNameByInstanceId.Clear();

        if (boardRoot == null)
        {
            return;
        }

        string sceneName = boardRoot.gameObject.scene.name;
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        BoardComponent[] all =
            FindObjectsByType<BoardComponent>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        var uniqueObjects = new Dictionary<int, GameObject>();
        for (int i = 0; i < all.Length; i++)
        {
            BoardComponent bc = all[i];
            if (bc == null || bc.gameObject == null)
            {
                continue;
            }

            GameObject go = bc.gameObject;
            if (!go.scene.IsValid() || !go.scene.isLoaded)
            {
                continue;
            }

            if (!string.Equals(go.scene.name, sceneName))
            {
                continue;
            }

            uniqueObjects[go.GetInstanceID()] = go;
        }

        var byTag = new Dictionary<string, List<GameObject>>();
        foreach (GameObject go in uniqueObjects.Values)
        {
            if (go == null)
            {
                continue;
            }

            string tag = go.tag;
            if (string.IsNullOrWhiteSpace(tag) || tag == "Untagged")
            {
                tag = "Component";
            }

            if (!byTag.TryGetValue(tag, out List<GameObject> list) || list == null)
            {
                list = new List<GameObject>();
                byTag[tag] = list;
            }

            list.Add(go);
        }

        foreach (KeyValuePair<string, List<GameObject>> kvp in byTag)
        {
            string tag = kvp.Key;
            List<GameObject> list = kvp.Value;
            if (list == null || list.Count == 0)
            {
                continue;
            }

            list.Sort(CompareBoardObjectPlacement);

            for (int i = 0; i < list.Count; i++)
            {
                GameObject go = list[i];
                if (go == null)
                {
                    continue;
                }

                displayNameByInstanceId[go.GetInstanceID()] = $"{tag} {i + 1}";
            }
        }
    }

    private static int CompareBoardObjectPlacement(GameObject a, GameObject b)
    {
        if (a == null && b == null)
        {
            return 0;
        }
        if (a == null)
        {
            return 1;
        }
        if (b == null)
        {
            return -1;
        }

        Transform ta = a.transform;
        Transform tb = b.transform;
        if (ta == null && tb == null)
        {
            return 0;
        }
        if (ta == null)
        {
            return 1;
        }
        if (tb == null)
        {
            return -1;
        }

        Vector3 pa = ta.position;
        Vector3 pb = tb.position;

        int cmp = pa.x.CompareTo(pb.x);
        if (cmp != 0)
        {
            return cmp;
        }

        cmp = pa.z.CompareTo(pb.z);
        if (cmp != 0)
        {
            return cmp;
        }

        cmp = pa.y.CompareTo(pb.y);
        if (cmp != 0)
        {
            return cmp;
        }

        return a.GetInstanceID().CompareTo(b.GetInstanceID());
    }

    private string GetDisplayNameFor(BoardComponent component)
    {
        if (component == null || component.gameObject == null)
        {
            return string.Empty;
        }

        if (displayNameByInstanceId.TryGetValue(component.gameObject.GetInstanceID(), out string displayName)
            && !string.IsNullOrWhiteSpace(displayName))
        {
            return displayName;
        }

        string tag = component.gameObject.tag;
        if (!string.IsNullOrWhiteSpace(tag) && tag != "Untagged")
        {
            return tag;
        }

        return component.gameObject.name;
    }
}
