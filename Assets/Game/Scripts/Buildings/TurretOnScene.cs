using System;
using Unity.Mathematics;
using UnityEngine;
[RequireComponent(typeof(Outline))]
public class  TurretOnScene : BuildingOnScene
{
   public Transform TurretBarrel;
   public Transform TurretHead;
   public Transform[] TurretSpawn;
}