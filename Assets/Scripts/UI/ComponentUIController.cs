// Generated with Cursor AI (GPT-5.2), by OpenAI, 2026-02-24.
// Change: prewarm selection outlines for board components.
// DEPRECATED: replaced by UnifiedShopController (2026-03-27).
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

/// <summary>
/// DEPRECATED -- replaced by <see cref="UnifiedShopController"/>.
/// Board-pose and component-replacement logic has been moved into UnifiedShopController.
/// </summary>
[System.Obsolete("Use UnifiedShopController instead.")]
public class ComponentUIController : MonoBehaviour
{
    [SerializeField] private Vector3 newBoardPosition;
    [SerializeField] private Vector3 newBoardRotation;
    [SerializeField] private Vector3 newBoardScale;
    [SerializeField] private Vector3 originalBoardPosition;
    [SerializeField] private Vector3 originalBoardRotation;
    [SerializeField] private Vector3 originialBoardScale;
    [SerializeField] private BoardRoot boardRoot;
    public BoardComponent chosenComponent;
    [SerializeField] private int chosenComponentIndex = 0;
    [SerializeField] private List<BoardComponent> bumpers = new List<BoardComponent>();
    [SerializeField] private List<BoardComponent> targets = new List<BoardComponent>();
    [SerializeField] private List<BoardComponentDefinition> allComponentDefinitions = new List<BoardComponentDefinition>();
    public BoardComponentDefinition buyingComponentDefinition;
    [SerializeField] private BoardComponentDefinition selectedComponentDefinition;    
    [SerializeField] private UpgradeComponents[] upgradeComponentList;
    [SerializeField] private TextMeshProUGUI componentTypeText;

    [SerializeField] private TextMeshProUGUI selectedComponentDescription;
    [SerializeField] private float movementTreshold = .8f;
    [SerializeField] private UIScript uiScript;
    [SerializeField] private GameRulesManager gameRulesManager;
    [SerializeField] private bool selectingButtons = true;
    /*
    keepMoving is used to make sure that controllers don't make the UI move every frame
    on the UIMovement function. It's implemented in a strange way so that
    it checks if the previous frame the left stick was past the movementThreshold before
    actually moving where the UI is.
    */
    [SerializeField] private bool keepMoving;

    private readonly Dictionary<int, string> displayNameByInstanceId = new Dictionary<int, string>();

    void Awake()
    {
        boardRoot = FindAnyObjectByType<BoardRoot>();
        chosenComponent = FindAnyObjectByType<BoardComponent>();
        uiScript = FindAnyObjectByType<UIScript>();
        gameRulesManager = FindAnyObjectByType<GameRulesManager>();
        chosenComponentIndex = 0;
    }

    void OnEnable()
    {
        if (boardRoot == null)
        {
            boardRoot = FindAnyObjectByType<BoardRoot>();
        }
        if (boardRoot == null)
        {
            return;
        }
        
        if (chosenComponent == null)
        {
            chosenComponent = FindAnyObjectByType<BoardComponent>();
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

        BoardComponent[] boardComponents = FindObjectsByType<BoardComponent>(FindObjectsSortMode.None);
        foreach (BoardComponent boardComponent in boardComponents)
        {
            if (!boardComponent.gameObject || !boardComponent) return;
            if (!bumpers.Contains(boardComponent) && boardComponent.componentType == BoardComponentType.Bumper) {
                bumpers.Add(boardComponent);
            }

            if (!targets.Contains(boardComponent) && boardComponent.componentType == BoardComponentType.Target) {
                targets.Add(boardComponent);
            }
        }

        bumpers.RemoveAll(x => !x);
        targets.RemoveAll(x => !x);

        bumpers.Sort();
        targets.Sort();

        BuildDisplayNamesForCurrentBoardScene();
        PrewarmSelectionOutlinesForCurrentBoardScene();
        selectingButtons = true;
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

    public void OnUIMovement(InputValue context)
    {
        Vector2 moveVector = context.Get<Vector2>();
        if (selectingButtons) return;

        if (moveVector.x >= movementTreshold)
        {
            if (keepMoving)
            {
                keepMoving = false;
                chosenComponent.DeSelect();
                chosenComponentIndex++;
                Refresh();
            }

        } else if (moveVector.x <= -movementTreshold)
        {
            if (keepMoving)
            {
                keepMoving = false;
                chosenComponent.DeSelect();
                chosenComponentIndex--;
                Refresh();
            }
            
        } else
        {
            keepMoving = true;
        }
    }

    public void OnEnter()
    {
        if (selectingButtons) return;
        if (!gameRulesManager.TrySpendCoins((int)buyingComponentDefinition.Price)) return;

        GameObject newComponent = Instantiate(buyingComponentDefinition.Prefab, chosenComponent.transform.parent);
        newComponent.transform.position = chosenComponent.transform.position;
        newComponent.transform.rotation = chosenComponent.transform.rotation;
        newComponent.transform.localScale = chosenComponent.startingSize;
        newComponent.GetComponent<BoardComponent>().startingSize = chosenComponent.startingSize;
        if (newComponent.GetComponent<BoardComponent>().componentType == BoardComponentType.Bumper)
        {
            bumpers.Add(newComponent.GetComponent<BoardComponent>());
            bumpers.Remove(chosenComponent);
        } else
        {
            targets.Add(newComponent.GetComponent<BoardComponent>());
            targets.Remove(chosenComponent);
        }
        Destroy(chosenComponent.gameObject);
        Destroy(chosenComponent);

        bumpers.Sort();
        targets.Sort();
        Refresh();
    }

    public void OnBack()
    {
        if (selectingButtons) return;
        selectingButtons = true;
        uiScript.SelectButton();
        chosenComponent.DeSelect();
        selectedComponentDescription.text = "";
    }

    public void Refresh()
    {
        selectingButtons = false;
        chosenComponent.DeSelect();

        if (buyingComponentDefinition.ComponentType == BoardComponentType.Bumper)
        {
            chosenComponent = bumpers[Mathf.Abs(chosenComponentIndex) % bumpers.Count];
        } else if (buyingComponentDefinition.ComponentType == BoardComponentType.Target)
        {
            chosenComponent = targets[Mathf.Abs(chosenComponentIndex) % targets.Count];
        }

        chosenComponent.Select();
        chosenComponent.GetComponent<BoardComponentDefinitionLink>().TryGetDefinition(out selectedComponentDefinition);
        selectedComponentDescription.text = selectedComponentDefinition.Description;

        if (componentTypeText != null)
        {
            componentTypeText.text = GetDisplayNameFor(chosenComponent);
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

    private void PrewarmSelectionOutlinesForCurrentBoardScene()
    {
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

            bc.PrewarmSelectionOutline();
        }
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

    public void RefreshComponentOffers()
    {
        List<BoardComponentDefinition> unlocked =
            GetUnlockedDefinitions();

        if (unlocked.Count == 0)
        {
            foreach (UpgradeComponents uc
                in upgradeComponentList)
            {
                uc.gameObject.SetActive(false);
            }

            return;
        }

        foreach (UpgradeComponents uc
            in upgradeComponentList)
        {
            uc.gameObject.SetActive(true);
            uc.boardComponentDefinition =
                GetNewUpgrade(unlocked);
            uc.Refresh();
        }
    }

    private BoardComponentDefinition GetNewUpgrade(
        List<BoardComponentDefinition> pool)
    {
        const int maxAttempts = 30;

        for (int attempt = 0;
            attempt < maxAttempts;
            attempt++)
        {
            BoardComponentDefinition candidate =
                pool[Random.Range(0, pool.Count)];

            bool duplicate = false;

            foreach (UpgradeComponents uc
                in upgradeComponentList)
            {
                if (uc.boardComponentDefinition
                    == candidate)
                {
                    duplicate = true;
                    break;
                }
            }

            if (!duplicate)
            {
                return candidate;
            }
        }

        return pool[Random.Range(0, pool.Count)];
    }

    private List<BoardComponentDefinition>
        GetUnlockedDefinitions()
    {
        var result =
            new List<BoardComponentDefinition>();

        if (ProgressionService.Instance == null)
        {
            return new List<BoardComponentDefinition>(
                allComponentDefinitions);
        }

        for (int i = 0;
            i < allComponentDefinitions.Count;
            i++)
        {
            BoardComponentDefinition def =
                allComponentDefinitions[i];

            if (def == null)
            {
                continue;
            }

            if (ProgressionService.Instance
                .IsComponentUnlocked(def.Id))
            {
                result.Add(def);
            }
        }

        return result;
    }
}
