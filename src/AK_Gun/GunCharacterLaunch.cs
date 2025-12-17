using BepInEx.Logging;
using Photon.Pun;
using UnityEngine;

namespace AK_Gun;

public class GunCharacterLaunch : MonoBehaviour
{
    internal static ManualLogSource Logger { get; private set; } = null!;

    public float shotTime;

    private Character characterGettingShot;

    private Vector3 shotDirection;

    public void Start()
    {
        characterGettingShot = Character.localCharacter;
    }

    public void Update()
    {
        if (shotTime > 0f)
        {
            shotTime -= Time.deltaTime;
            UpdateShotPhysicsT();
        }
    }

    public void ShootSelfT(float howLongToFly, Character whoIsGettingShot, Vector3 whichDirectionShooting)
    {
        shotTime = howLongToFly;
        characterGettingShot = whoIsGettingShot;
        shotDirection = whichDirectionShooting;
    }
    
    [PunRPC]
    public void RPC_ShootSelfT(float howLongToFly, int CharacterViewID, Vector3 whichDirectionShooting)
    {
        PhotonView targetView = PhotonView.Find(CharacterViewID);
        if (targetView != null)
        {
            Character character = targetView.GetComponent<Character>();
            if (character != null)
            {
                characterGettingShot = character;
            }
            else
            {
                Debug.LogError($"Character {CharacterViewID} not found");
                characterGettingShot = Character.localCharacter;
            }
        }
        else
        {
            Debug.LogError($"PhotonView {CharacterViewID} not found");
            characterGettingShot = Character.localCharacter;
        }
        
        shotTime = howLongToFly;
        
        shotDirection = whichDirectionShooting;
    }

    public void UpdateShotPhysicsT()
    {
        Vector3 ForceDirection = shotDirection * 25f * -1f;
        characterGettingShot.Fall(0.5f, 0f);
        characterGettingShot.AddForce(ForceDirection, 1f, 1f);
    }
}