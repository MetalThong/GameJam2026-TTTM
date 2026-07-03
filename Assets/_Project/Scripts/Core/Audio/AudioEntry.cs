using System;
using UnityEngine;

[Serializable]
public sealed class AudioEntry
{
    [SerializeField] private string id;
    [SerializeField] private AudioClip clip;

    public string Id => id;
    public AudioClip Clip => clip;
}
