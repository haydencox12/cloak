using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Avatar : MonoBehaviour
{
    [SerializeField]
    Image image;
    [SerializeField]
    List<Sprite> sprites;

    // Start is called before the first frame update
    void Start()
    {
        // Randomize avatar
        image.sprite = sprites[Random.Range(0, sprites.Count)];
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
