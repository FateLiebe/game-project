using UnityEngine;

public class ShopTrigger : MonoBehaviour
{
    [SerializeField] private GameObject interactPrompt;
    private bool isPlayerInRange = false;

    private void Start()
    {
        if (interactPrompt != null)
            interactPrompt.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player") || collision.transform.root.CompareTag("Player"))
        {
            isPlayerInRange = true;
            if (interactPrompt != null)
                interactPrompt.SetActive(true);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player") || collision.transform.root.CompareTag("Player"))
        {
            isPlayerInRange = false;
            if (interactPrompt != null)
                interactPrompt.SetActive(false);
                
            // Tắt shop nếu đang mở
            if (ShopManager.Instance != null && ShopManager.Instance.isShopOpen)
            {
                ShopManager.Instance.CloseShop();
            }
        }
    }

    private void Update()
    {
        if (isPlayerInRange && Input.GetKeyDown(KeyCode.R))
        {
            if (ShopManager.Instance != null)
            {
                if (!ShopManager.Instance.isShopOpen)
                    ShopManager.Instance.OpenShop();
                else
                    ShopManager.Instance.CloseShop();
            }
        }
    }
}
