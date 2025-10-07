using Peak.Afflictions;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace AK_Gun;

public class Action_Gun : ItemAction
{
	public float maxDistance;

	public float dartCollisionSize;

	[SerializeReference]
	public Affliction[] afflictionsOnHit;

	public Transform spawnTransform;

	public GameObject dartVFX;

	private HelperFunctions.LayerType layerMaskType;

	private RaycastHit lineHit;

	private RaycastHit[] sphereHits;
	
	private RaycastHit[] itemSphereHits;

	private float lastShootTime = 0f;

	public float fireRate = 0.5f;

	public SFX_Instance shotSFX;

	private Item lastHitItem;

	public override void RunAction()
	{
		if (Time.time > lastShootTime + fireRate)
		{
			Debug.Log("Shot, firerate:" + fireRate + ". lastShootTime:" + lastShootTime);
			lastShootTime = Time.time;
			FireGun();
		}
		// else
		// {
		// 	Debug.Log("Failed shot, firerate:" + fireRate + ". lastShootTime:" + lastShootTime);
		// }
		
	}

	private void OnDrawGizmosSelected()
	{
		Gizmos.color = Color.red;
		Gizmos.DrawWireSphere(spawnTransform.position, dartCollisionSize);
	}

	private void FireGun()
	{
		if ((bool)shotSFX)
		{
			shotSFX.Play(base.transform.position);
		}
		Physics.Raycast(spawnTransform.position, MainCamera.instance.transform.forward, out lineHit, maxDistance, HelperFunctions.terrainMapMask, QueryTriggerInteraction.Ignore);
		if (!lineHit.collider)
		{
			lineHit.distance = maxDistance;
			lineHit.point = spawnTransform.position + MainCamera.instance.transform.forward * maxDistance;
		}
		sphereHits = Physics.SphereCastAll(spawnTransform.position, dartCollisionSize, MainCamera.instance.transform.forward, lineHit.distance, LayerMask.GetMask("Character"), QueryTriggerInteraction.Ignore);
		RaycastHit[] array = sphereHits;
		for (int i = 0; i < array.Length; i++)
		{
			RaycastHit raycastHit = array[i];
			if (!raycastHit.collider)
			{
				continue;
			}
			Character componentInParent = raycastHit.collider.GetComponentInParent<Character>();
			// Item componentInItem = raycastHit.collider.GetComponentInParent<Item>();
			Debug.Log("Character: " + componentInParent);
			// Debug.Log("Item: " + componentInItem);
			if ((bool)componentInParent)
			{
				Debug.Log("HIT");
				if (componentInParent != base.character)
				{
					GunImpact(componentInParent, spawnTransform.position, raycastHit.point);
					return;
				}
			} 
			// else if ((bool)componentInItem)
			// {
			// 	Debug.Log("Item was hit!");
			// 	GunItemImpact(componentInItem, spawnTransform.position, raycastHit.point);
			// }
		}
		
		// itemSphereHits = Physics.SphereCastAll(spawnTransform.position, dartCollisionSize, MainCamera.instance.transform.forward, lineHit.distance, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore);
		// RaycastHit[] itemArray = itemSphereHits;
		// for (int i = 0; i < itemArray.Length; i++)
		// {
		// 	RaycastHit raycastHit = itemArray[i];
		// 	if (!raycastHit.collider)
		// 	{
		// 		continue;
		// 	}
		// 	Item componentInItem = raycastHit.collider.GetComponentInParent<Item>();
		// 	Debug.Log("Item: " + componentInItem);
		// 	if ((bool)componentInItem && componentInItem != this.GetComponentInParent<Item>())
		// 	{
		// 		Debug.Log("Item was hit!");
		// 		GunItemImpact(componentInItem, spawnTransform.position, raycastHit.point);
		// 		return;
		// 	}
		// }
	}

	private void GunImpact(Character hitCharacter, Vector3 origin, Vector3 endpoint)
	{
		if ((bool)hitCharacter)
		{
			base.photonView.RPC("RPC_GunImpact", RpcTarget.All, hitCharacter.photonView.Owner, origin, endpoint);
		}
		else
		{
			base.photonView.RPC("RPC_GunImpact", RpcTarget.All, null, origin, endpoint);
		}
	}

	private void GunItemImpact(Item hitItem, Vector3 origin, Vector3 endpoint)
	{
		lastHitItem = hitItem;
		base.photonView.RPC("RPC_GunItemImpact", RpcTarget.All, origin, endpoint, MainCamera.instance.transform.forward);
		// RPC_GunItemImpact(origin, endpoint, MainCamera.instance.transform.forward);
	}

	[PunRPC]
	private void RPC_GunImpact(Photon.Realtime.Player hitPlayer, Vector3 origin, Vector3 endpoint)
	{
		if (hitPlayer != null && hitPlayer.IsLocal)
		{
			Debug.Log("I'M HIT");
			Affliction[] array = afflictionsOnHit;
			foreach (Affliction affliction in array)
			{
				Character.localCharacter.refs.afflictions.AddAffliction(affliction);
			}
		}
		Object.Instantiate(dartVFX, endpoint, Quaternion.identity);
		GamefeelHandler.instance.AddPerlinShakeProximity(endpoint, 5f);
	}
	
	[PunRPC]
	private void RPC_GunItemImpact(Vector3 origin, Vector3 endpoint, Vector3 direction)
	{
		if (lastHitItem)
		{
			Photon.Realtime.Player oldOwner = lastHitItem.photonView.Owner;
			lastHitItem.photonView.TransferOwnership(GetComponent<PhotonView>().Owner);
			Debug.Log("Transferred ownership of the item's PhotonView.");
			
			Vector3 newVelocity = direction * 25;
			lastHitItem.GetComponent<Rigidbody>().AddForce(newVelocity.x, newVelocity.y, newVelocity.z, ForceMode.VelocityChange);
			Debug.Log("Item launched");
			
			lastHitItem.photonView.TransferOwnership(0);
			Debug.Log("Transferred ownership of the item's PhotonView to ViewID 0.");
		}
		Object.Instantiate(dartVFX, endpoint, Quaternion.identity);
		Debug.Log("Spawning DartVFX");
		GamefeelHandler.instance.AddPerlinShakeProximity(endpoint, 5f);
		Debug.Log("Playing PerlinShake.");
	}
}
