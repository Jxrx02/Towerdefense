
using System;
using System.Collections;
using System.Collections.Generic;
using TowerDefense;
using TowerDefense.GridMovement;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class TowerShop : MonoBehaviour
{
    [Header("Tower UI")]
    public LevelManager levelManager; //TODO: Consider costs when buying

    private GameObject selectedTower;
    private bool isPlacingTower = false; // Flag, um zu prüfen, ob der Tower gerade platziert wird
	private Camera _mainCamera;
    

	private void Awake()
	{
		_mainCamera = Camera.main;
	}

    public void BuyTower(GameObject towerPrefab)
    {
        if (selectedTower != null) return;
        
        var tow = towerPrefab.GetComponent<Tower>();
        if (tow is Hero && LevelManager.instance.heroFielded) return;

        if (LevelManager.instance.DoPurchase(tow.towerInitPrice) == false)
        {
            selectedTower = null;
            isPlacingTower = false;
            return;
        }
        
        GameObject tower = Instantiate(towerPrefab);
        selectedTower = tower;
        isPlacingTower = true;
        var listener = tower.GetComponent<OnTowerClickListener>();
        if (listener != null) listener._enabled = false;

        tower.GetComponent<Collider2D>().enabled = false;
        
        //setzte isAttacking true, um Angriff zu meiden
        if (selectedTower.TryGetComponent(out Hero hero))
        {
            hero.isAttacking = true;
        }
    }
    
    private void PlaceTower()
    {
        if (selectedTower != null && isPlacingTower)
        {
            TowerHeroManager.instance.RegisterTower(selectedTower);

            selectedTower.GetComponent<Collider2D>().enabled = true;
            
            if (selectedTower.TryGetComponent(out Tower tower))
                tower.SetIsSelected(false);

            if (selectedTower.TryGetComponent(out Hero hero))
            {
                hero.SetIsSelected(false);
                hero.transform.position = mousePosition;
                hero.isAttacking = false;
                
                levelManager.heroFielded = true;
            }
            
            if (selectedTower.TryGetComponent(out WallGroup wall))
            {
               // wall.SetIsSelected(false);
               // wall.isAttacking = false;

                //wall.InitializeBuiltWall();
            }

            selectedTower = null;
            isPlacingTower = false;
        }
    }

    private Vector3 mousePosition;
    private void Update()
    {
        if (!isPlacingTower || selectedTower == null)
            return;

        mousePosition = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
        mousePosition.z = 0;

        Tower towerComp = selectedTower.GetComponent<Tower>();
        bool valid = false;

        // ───────── WALL VALIDATION ─────────
        if (towerComp is WallSegment)
        {
            // ───────── SNAP ─────────
            selectedTower.transform.position = GridManager.Instance.SnapToGrid(mousePosition);

            valid = GridManager.Instance.CanPlaceWall(selectedTower.transform.position);

            selectedTower.GetComponent<SpriteRenderer>().color =
                valid ? Color.white : Color.red;
        }
        else{
            selectedTower.transform.position = mousePosition;
            valid = true;

        } 


        if (Input.GetMouseButtonDown(0) && valid)
        {
            TryPlaceTower(towerComp);
        } 
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            CancelPlacement();
        }
        /*OneClickInWorldListener.ListenOnce((Vector3 pos) =>
        {
            TryPlaceTower(towerComp);
        });*/
    }
    
    private void TryPlaceTower(Tower tower)
    {
        if (selectedTower == null || tower == null)
            return;


        if (tower is WallSegment)
        {
            Vector3 pos = tower.transform.position;

            if (!GridManager.Instance.CanPlaceWall(pos))
            {
                selectedTower.GetComponent<SpriteRenderer>().color = Color.red;
                tower.GetComponent<OnTowerClickListener>()._enabled = false;

                return;
            }

            GridManager.Instance.PlaceWall(pos);
            selectedTower.GetComponent<SpriteRenderer>().color = Color.white;
        }
        
        tower.GetComponent<OnTowerClickListener>()._enabled = true;
        PlaceTower();
    }
    

    private void CancelPlacement()
    {
        if (selectedTower == null)
            return;

        // Geld zurückgeben
        Tower tower = selectedTower.GetComponent<Tower>();
        LevelManager.instance.cur_coins += tower.towerInitPrice;

        Destroy(selectedTower);

        selectedTower = null;
        isPlacingTower = false;
    }
    
    
}
