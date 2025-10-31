using UnityEngine;
using UnityEngine.UI;

namespace ElitesAndPawns.UI
{
    /// <summary>
    /// Debug and fix HUD layout
    /// </summary>
    public class HUDDebugger : MonoBehaviour
    {
        private void Start()
        {
            Debug.Log("=== HUD LAYOUT FIX ===");
            DiagnoseAmmoPanel();
            FixHUDLayout();
        }
        
        private void DiagnoseAmmoPanel()
        {
            Debug.Log("=== AMMO PANEL DIAGNOSIS ===");
            
            RectTransform ammoPanel = transform.Find("AmmoPanel") as RectTransform;
            if (ammoPanel != null)
            {
                Debug.Log($"AmmoPanel found! Active: {ammoPanel.gameObject.activeSelf}");
                Debug.Log($"AmmoPanel position: {ammoPanel.anchoredPosition}");
                Debug.Log($"AmmoPanel anchors: Min={ammoPanel.anchorMin}, Max={ammoPanel.anchorMax}");
                Debug.Log($"AmmoPanel size: {ammoPanel.sizeDelta}");
                Debug.Log($"AmmoPanel children: {ammoPanel.childCount}");
                
                foreach (Transform child in ammoPanel)
                {
                    Debug.Log($"  Child: {child.name}, Active: {child.gameObject.activeSelf}");
                    Text text = child.GetComponent<Text>();
                    if (text != null)
                    {
                        Debug.Log($"    Text: '{text.text}', FontSize: {text.fontSize}, Color: {text.color}, Enabled: {text.enabled}");
                        RectTransform rect = text.GetComponent<RectTransform>();
                        Debug.Log($"    Position: {rect.anchoredPosition}, Size: {rect.sizeDelta}");
                    }
                }
            }
            else
            {
                Debug.LogError("AmmoPanel NOT FOUND!");
            }
        }
        
        private void FixHUDLayout()
        {
            // Fix HealthPanel - bottom left
            RectTransform healthPanel = transform.Find("HealthPanel") as RectTransform;
            if (healthPanel != null)
            {
                healthPanel.anchorMin = new Vector2(0, 0);
                healthPanel.anchorMax = new Vector2(0, 0);
                healthPanel.pivot = new Vector2(0, 0);
                healthPanel.anchoredPosition = new Vector2(20, 20);
                healthPanel.sizeDelta = new Vector2(250, 80);
                
                // Make health bar background visible but subtle
                Transform bgTransform = healthPanel.Find("HealthBar_Background");
                if (bgTransform != null)
                {
                    Image bgImage = bgTransform.GetComponent<Image>();
                    if (bgImage != null)
                    {
                        bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
                    }
                }
                
                // Fix health text
                Transform healthTextTransform = healthPanel.Find("HealthText");
                if (healthTextTransform != null)
                {
                    Text healthText = healthTextTransform.GetComponent<Text>();
                    if (healthText != null)
                    {
                        healthText.alignment = TextAnchor.MiddleCenter;
                        healthText.fontSize = 32;
                        healthText.color = Color.white;
                    }
                }
            }
            
            // Fix AmmoPanel - bottom right
            RectTransform ammoPanel = transform.Find("AmmoPanel") as RectTransform;
            if (ammoPanel != null)
            {
                Debug.Log("Fixing AmmoPanel positioning...");
                
                ammoPanel.anchorMin = new Vector2(1, 0);
                ammoPanel.anchorMax = new Vector2(1, 0);
                ammoPanel.pivot = new Vector2(1, 0);
                ammoPanel.anchoredPosition = new Vector2(-20, 20);
                ammoPanel.sizeDelta = new Vector2(250, 120);
                
                // Make sure it's active
                ammoPanel.gameObject.SetActive(true);
                
                Debug.Log($"AmmoPanel repositioned to: {ammoPanel.anchoredPosition}");
                
                // Fix weapon name text
                Transform weaponTextTransform = ammoPanel.Find("WeaponNameText");
                if (weaponTextTransform != null)
                {
                    Text weaponText = weaponTextTransform.GetComponent<Text>();
                    RectTransform weaponRect = weaponTextTransform.GetComponent<RectTransform>();
                    
                    if (weaponText != null && weaponRect != null)
                    {
                        weaponRect.anchorMin = new Vector2(0, 1);
                        weaponRect.anchorMax = new Vector2(1, 1);
                        weaponRect.pivot = new Vector2(0.5f, 1);
                        weaponRect.anchoredPosition = new Vector2(0, -5);
                        weaponRect.sizeDelta = new Vector2(0, 30);
                        
                        weaponText.alignment = TextAnchor.UpperCenter;
                        weaponText.fontSize = 20;
                        weaponText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
                        
                        Debug.Log($"WeaponNameText fixed: '{weaponText.text}'");
                    }
                }
                else
                {
                    Debug.LogError("WeaponNameText NOT FOUND!");
                }
                
                // Fix ammo text
                Transform ammoTextTransform = ammoPanel.Find("AmmoText");
                if (ammoTextTransform != null)
                {
                    Text ammoText = ammoTextTransform.GetComponent<Text>();
                    RectTransform ammoRect = ammoTextTransform.GetComponent<RectTransform>();
                    
                    if (ammoText != null && ammoRect != null)
                    {
                        ammoRect.anchorMin = new Vector2(0, 0);
                        ammoRect.anchorMax = new Vector2(1, 1);
                        ammoRect.pivot = new Vector2(0.5f, 0.5f);
                        ammoRect.anchoredPosition = new Vector2(0, -15);
                        ammoRect.sizeDelta = new Vector2(0, -35);
                        
                        ammoText.alignment = TextAnchor.MiddleCenter;
                        ammoText.fontSize = 48;
                        ammoText.fontStyle = FontStyle.Bold;
                        ammoText.color = Color.white;
                        
                        Debug.Log($"AmmoText fixed: '{ammoText.text}'");
                    }
                }
                else
                {
                    Debug.LogError("AmmoText NOT FOUND!");
                }
            }
            else
            {
                Debug.LogError("AmmoPanel NOT FOUND in FixHUDLayout!");
            }
            
            Debug.Log("=== HUD LAYOUT FIX COMPLETE ===");
        }
    }
}
