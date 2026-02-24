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
        componentTypeText.text = chosenComponent.tag;
        UpgradeComponents[] upgradeComponents = GetComponentsInChildren<UpgradeComponents>();

        foreach(UpgradeComponents upgrade in upgradeComponents)
        {
            upgrade.Refresh(chosenComponent.gameObject);
        }
    }
}
