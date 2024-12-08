using TMPro;
using UnityEngine;

public class AmmoController : MonoBehaviour
{
    private int amount, capacity;
    private TMP_Text AmountText, CapacityText;

    public int Amount
    {
        get => amount;
        set
        {
            amount = value;
            AmountText.text = amount.ToString();
        }
    }

    private void Awake()
    {
        AmountText = transform.Find("Amount").GetComponent<TMP_Text>();
        CapacityText = transform.Find("Capacity").GetComponent<TMP_Text>();
    }

    public int Capacity
    {
        get => capacity;
        set
        {
            capacity = value;
            CapacityText.text = capacity.ToString();
        }
    }
}
