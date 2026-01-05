using UnityEngine;

[CreateAssetMenu(menuName = "Game/Weapons/Weapon Data")]
public class WeaponData : ScriptableObject
{
    public string weaponName = "Knife";

    [Header("Prefab")]
    public GameObject projectilePrefab;

    [Header("Stats")]
    public float damage = 10f;
    public float cooldown = 0.5f;
    public float projectileSpeed = 12f;
    public float lifetime = 2f;
    public int pierce = 1;

    [Header("Targeting")]
    public bool aimAtNearestEnemy = true;

    [Tooltip("If aimAtNearestEnemy is true, only look for enemies within this range.")]
    public float targetRange = 20f;
}
