using System.Buffers;
using System.Collections.Generic;
using System;
using System.Collections;
using System.Linq;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Logging;
using Peak.Afflictions;
using PEAKLib.Core;
using PEAKLib.Items;
using PEAKLib.Items.UnityEditor;
using Photon.Pun;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
// using UnityExplorer.UI.Panels.LogPanel;

namespace AK_Gun;

// Here are some basic resources on code style and naming conventions to help
// you in your first CSharp plugin!
// https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions
// https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/identifier-names
// https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/names-of-namespaces

// This BepInAutoPlugin attribute comes from the Hamunii.BepInEx.AutoPlugin
// NuGet package, and it will generate the BepInPlugin attribute for you!
// For more info, see https://github.com/Hamunii/BepInEx.AutoPlugin
[BepInAutoPlugin]
[BepInDependency(ItemsPlugin.Id)] // PEAKLib.Items
[BepInDependency(CorePlugin.Id)] // PEAKLib.Core, a dependency of PEAKLib.Items
public partial class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log { get; private set; } = null!;

    private Coroutine? LevelLoadCompleteCoroutine;
    private static class Util
	{
		public static IEnumerable<GameObject> GetChildrenOf(GameObject target)
		{
			Transform transform = target.GetComponent<Transform>();
			if ((object)transform == null)
			{
				return Array.Empty<GameObject>();
			}
			return from Transform childTransform in transform
				select childTransform.gameObject;
		}

		public static Mesh EnsureMeshReadable(Mesh mesh)
		{
			if (!mesh.isReadable)
			{
				return MakeReadableMeshCopy(mesh);
			}
			return mesh;
		}

		public static Mesh MakeReadableMeshCopy(Mesh nonReadableMesh)
		{
			Mesh meshCopy = new Mesh();
			meshCopy.indexFormat = nonReadableMesh.indexFormat;
			GraphicsBuffer verticesBuffer = nonReadableMesh.GetVertexBuffer(0);
			int totalSize = verticesBuffer.stride * verticesBuffer.count;
			byte[] data = new byte[totalSize];
			verticesBuffer.GetData(data);
			meshCopy.SetVertexBufferParams(nonReadableMesh.vertexCount, nonReadableMesh.GetVertexAttributes());
			meshCopy.SetVertexBufferData(data, 0, 0, totalSize);
			verticesBuffer.Release();
			meshCopy.subMeshCount = nonReadableMesh.subMeshCount;
			GraphicsBuffer indexesBuffer = nonReadableMesh.GetIndexBuffer();
			int tot = indexesBuffer.stride * indexesBuffer.count;
			byte[] indexesData = new byte[tot];
			indexesBuffer.GetData(indexesData);
			meshCopy.SetIndexBufferParams(indexesBuffer.count, nonReadableMesh.indexFormat);
			meshCopy.SetIndexBufferData(indexesData, 0, 0, tot);
			indexesBuffer.Release();
			uint currentIndexOffset = 0u;
			for (int i = 0; i < meshCopy.subMeshCount; i++)
			{
				uint subMeshIndexCount = nonReadableMesh.GetIndexCount(i);
				meshCopy.SetSubMesh(i, new SubMeshDescriptor((int)currentIndexOffset, (int)subMeshIndexCount));
				currentIndexOffset += subMeshIndexCount;
			}
			meshCopy.RecalculateNormals();
			meshCopy.RecalculateBounds();
			return meshCopy;
		}
	}

	private class SceneTreeQueryNode
	{
		public class Epsilon : SceneTreeQueryNode
		{
			protected override void Run(GameObject target, int depth)
			{
				foreach (SceneTreeQueryNode child in Children)
				{
					child.Run(target, depth);
				}
			}
		}
		
		private Predicate<GameObject> predicate;
		
		private Action<GameObject> action;

		private readonly List<SceneTreeQueryNode> Children;

		public SceneTreeQueryNode(Predicate<GameObject>? Predicate = null, Action<GameObject>? Action = null)
		{
			predicate = Predicate;
			action = Action;
			Children = new List<SceneTreeQueryNode>();
			// base..ctor();
		}

		public bool Matches(GameObject target)
		{
			if (predicate != null)
			{
				return predicate.Invoke(target);
			}
			else
			{
				return true;
			}
			// return predicate.Invoke(target) ?? true;
		}

		protected virtual void Run(GameObject target, int depth)
		{
			action.Invoke(target);
			foreach (var (childGameObject2, childNode2) in from GameObject childGameObject in Util.GetChildrenOf(target)
				from childNode in Children.Cast<SceneTreeQueryNode>()
				where childNode.Matches(childGameObject)
				select (childGameObject, childNode))
			{
				childNode2.Run(childGameObject2);
			}
		}

		public void Run(GameObject target)
		{
			Run(target, 0);
		}

		public void Run(IEnumerable<GameObject> targets)
		{
			foreach (GameObject target in targets)
			{
				Run(target);
			}
		}

		public SceneTreeQueryNode Tap(Action<SceneTreeQueryNode> f)
		{
			f(this);
			return this;
		}

		public SceneTreeQueryNode Child(SceneTreeQueryNode child)
		{
			Children.Add(child);
			return child;
		}

		public SceneTreeQueryNode Child(Predicate<GameObject> predicate)
		{
			return Child(new SceneTreeQueryNode(predicate));
		}

		public SceneTreeQueryNode Child(Predicate<GameObject> predicate, Action<GameObject> action)
		{
			return Child(new SceneTreeQueryNode(predicate, action));
		}

		public void Tee(params Action<SceneTreeQueryNode>[] tees)
		{
			foreach (Action<SceneTreeQueryNode> tee in tees)
			{
				tee(this);
			}
		}
	}
	
	private static GameObject AK;
    private void Awake()
    {
        // BepInEx gives us a logger which we can use to log information.
        // See https://lethal.wiki/dev/fundamentals/logging
        Log = Logger;

        #region Instructions
        // BepInEx also gives us a config file for easy configuration.
        // See https://lethal.wiki/dev/intermediate/custom-configs

        // We can apply our hooks here.
        // See https://lethal.wiki/dev/fundamentals/patching-code
        #endregion
        
        Log.LogInfo("Patching sceneLoaded...");
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        
        LocalizationFix();
        
        this.LoadBundleWithName(
            "ak.peakbundle",
            peakBundle =>
            {
                var AKContent = peakBundle.LoadAsset<UnityItemContent>("IC_AK");
                var ShotSFX = peakBundle.LoadAsset<SFX_Instance>("SFX_AK");
                var ShotVFX = peakBundle.LoadAsset<GameObject>("VFX_AK");
                AK = AKContent.ItemPrefab;
                
                // AK_Bundle = peakBundle;
        
                // var raycastdart = AK.GetComponent<Action_RaycastDart>();
                // raycastdart.afflictionsOnHit[0] = new Affliction_AdjustStatus(CharacterAfflictions.STATUSTYPE.Injury, 0.2f, 1);
                // Log.LogInfo(raycastdart.afflictionsOnHit[0].ToString());
                
                var actionGun = AK.AddComponent<Action_Gun>();
                actionGun.maxDistance = 500f;
                actionGun.dartCollisionSize = 0.3f;
                actionGun.fireRate = 0.15f;
                actionGun.OnHeld = true;
                actionGun.spawnTransform = AK.transform.FindChild("SpawnPos").transform;
                actionGun.shotSFX = ShotSFX;
                actionGun.dartVFX = ShotVFX;
                actionGun.afflictionsOnHit = new Affliction[1];
                actionGun.afflictionsOnHit[0] = new Affliction_AdjustStatus(CharacterAfflictions.STATUSTYPE.Injury, 0.1f, 1);
                Log.LogInfo(actionGun.afflictionsOnHit[0].ToString());
                
                // var AKvfx = AK.AddComponent<AKVFX>();
                // AKvfx.akParticles = AK.transform.FindChild("VFX_Gunshot").gameObject.GetComponent<ParticleSystem>();
                
                peakBundle.Mod.RegisterContent();
            }
        );
        
        Log.LogInfo($"Plugin {Name} is loaded!");
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
	    Log.LogInfo("Loaded scene: " + scene.path);
	    if (scene.path.StartsWith("Assets/8_SCENES/Generated/"))
	    {
		    Log.LogInfo("started loading level scenes...");
		    if (LevelLoadCompleteCoroutine != null)
		    {
			    StopCoroutine(LevelLoadCompleteCoroutine);
			    LevelLoadCompleteCoroutine = null;
		    }
		    LevelLoadCompleteCoroutine = StartCoroutine(PatchAfterLoadFinished());
	    }
	    IEnumerator PatchAfterLoadFinished()
	    {
		    while (LoadingScreenHandler.loading)
		    {
			    yield return null;
		    }
		    Log.LogInfo("patching level scene...: " + scene.path.Remove("Assets/8_SCENES/Generated/".Length));
		    createItemSpawners();
	    }
    }

    private static void createItemSpawners()
    {
		GameObject bingbong = GameObject.Find("BingBong_Spawner");
		GameObject ak_spawner = UnityEngine.Object.Instantiate(bingbong, bingbong.transform.parent);
		ak_spawner.SetActive(false);
		ak_spawner.name = "AK_Spawner";
		ak_spawner.transform.localPosition = new Vector3(-10.862f, 0.7437f, -4.13f);
		
		// -10.9056 1.5401 -5.6609 - Martlet Plush
		// -13.0111 1.5401 -5.9445 - Zenith Martlet Plush
		// -13.3275 1.351 -3.2354 - Keet Plush
		// 270 174.7915 0 - Plush Rotation
		Log.LogInfo("Spawning " + ak_spawner.name);
		
		try
		{
			ak_spawner.GetComponent<SingleItemSpawner>().prefab = AK;
			Log.LogInfo("Setting " + ak_spawner.name + "'s prefab to " + ak_spawner.GetComponent<SingleItemSpawner>().prefab.name);
			ak_spawner.SetActive(true);
			ak_spawner.GetComponent<SingleItemSpawner>().TrySpawnItems();
		}
		catch (Exception e)
		{
			Log.LogInfo("Failed to spawn " + ak_spawner.name + ": " + e.Message);
		}
    }
    
    // Unused Code
    //
    	 //    new SceneTreeQueryNode.Epsilon().Tap(delegate(SceneTreeQueryNode n)
		// {
		// 	n.Child((GameObject o) => (object)o != null && o.name == "Map").Tee( delegate(SceneTreeQueryNode n)
		// 	{
		// 		n.Child((GameObject o) => (object)o != null && o.name == "Biome_1").Tee(delegate(SceneTreeQueryNode n)
		// 		{
		// 			n.Child((GameObject o) => (object)o != null && o.name == "Beach").Tee(delegate(SceneTreeQueryNode n)
		// 			{
		// 				n.Child((GameObject o) => (object)o != null && o.name == "Beach_Segment").Tee(delegate(SceneTreeQueryNode n)
		// 				{
		// 					n.Child((GameObject o) => (object)o != null && o.name == "crashed plane").Tee(delegate(SceneTreeQueryNode n)
		// 					{
		// 						n.Child(delegate(GameObject o)
		// 						{
		// 							if ((object)o != null)
		// 							{
		// 								string text3 = o.name;
		// 								if (text3 == "BingBong_Spawner")
		// 								{
		// 									Log.LogInfo("Match found for spawner object");
		// 									return true;
		// 								}
		// 							}
		// 							return false;
		// 						}, delegate(GameObject o)
		// 						{
		// 							GameObject test_spawner = UnityEngine.Object.Instantiate(o);
		// 							test_spawner.name = "AK_Spawner";
		// 							Log.LogInfo("Spawning " + test_spawner.name);
		// 							
		// 							test_spawner.GetComponent<SingleItemSpawner>().prefab = Resources.Load<GameObject>("_AK/AK");
		// 							Log.LogInfo("Setting " + test_spawner.name + "'s prefab to " + test_spawner.GetComponent<SingleItemSpawner>().prefab.name);
		// 						});
		// 					});
		// 				});
		// 			});
		// 		});
		// 		// n.Child(delegate(GameObject o)
		// 		// {
		// 		// 	if ((object)o != null)
		// 		// 	{
		// 		// 		string text3 = o.name;
		// 		// 		if (text3 == "OutofBoundsBlockers" || text3 == "OutofBoundsBlockers (1)")
		// 		// 		{
		// 		// 			return true;
		// 		// 		}
		// 		// 	}
		// 		// 	return false;
		// 		// }, delegate(GameObject o)
		// 		// {
		// 		// 	o.SetActive(value: false);
		// 		// });
		// 			//, delegate(SceneTreeQueryNode n)
		// 		// {
		// 		// 	n.Child((GameObject o) => (object)o != null && o.name == "Lights").Child((GameObject _) => true);
		// 		// }, delegate(SceneTreeQueryNode n)
		// 		// {
		// 		// 	n.Child((GameObject o) => (object)o != null && o.name == "Displays").Child((GameObject _) => true);
		// 		// }, delegate(SceneTreeQueryNode n)
		// 		// {
		// 		// 	n.Child((GameObject o) => (object)o != null && o.name == "GlassFence");
		// 		// }, delegate(SceneTreeQueryNode n)
		// 		// {
		// 		// 	n.Child(delegate(GameObject o)
		// 		// 	{
		// 		// 		if ((object)o != null)
		// 		// 		{
		// 		// 			switch (o.name)
		// 		// 			{
		// 		// 			case "Plane":
		// 		// 			case "Plane (1)":
		// 		// 			case "Plane (2)":
		// 		// 				return true;
		// 		// 			}
		// 		// 		}
		// 		// 		return false;
		// 		// 	}).Child((GameObject _) => true);
		// 		// }
	 //
		// 	});
		// }).Run(scene.GetRootGameObjects());
    private static void LocalizationFix()
	{
		LocalizedText.mainTable["NAME_AK"] = new List<string>(15)
		{
			"AK-47", "AK-47", "AK-47", "AK-47", "AK-47", "AK-47", "AK-47", "AK-47", "AK-47", "AK-47",
			"AK-47", "AK-47", "AK-47", "AK-47", "AK-47"
		};
	}
}
