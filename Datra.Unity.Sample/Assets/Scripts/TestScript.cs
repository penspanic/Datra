using Datra.Attributes;
using UnityEngine;

public class TestScript : MonoBehaviour
{
    [ReadOnlyInInspector] public int publicReadOnlyInt;
    [ReadOnlyInInspector][SerializeField] private int privateReadOnlyInt;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
