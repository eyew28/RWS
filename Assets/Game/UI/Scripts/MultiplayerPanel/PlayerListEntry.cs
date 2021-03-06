﻿using TMPro;
using UnityEngine;

public class PlayerListEntry : MonoBehaviour
{
    [SerializeField]
    TextMeshProUGUI numberText = null;
    
    [SerializeField]
    TextMeshProUGUI nameText = null;


    public void Init( int number, string name )
    {
        numberText.text = $"{number}.";
        nameText.text = name;
    }
}
