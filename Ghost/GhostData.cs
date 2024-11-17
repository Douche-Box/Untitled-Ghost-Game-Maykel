using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "GhostData", menuName = "ScriptableObjects/GhostData", order = 1)]
public class GhostData : ScriptableObject
{
    [Tooltip("Points gained from collecting this ghost")]
    public int points;
    [Tooltip("Speed at which this ghost moves")]
    public float speed;
    [Tooltip("Maximum range this ghost can move")]
    public float walkradius;
    [Tooltip("Time it takes to collect this ghost")]
    public int suckTime;
    [Tooltip("Rate at which this ghost regains strenght")]
    public int rechargeRate;

}
