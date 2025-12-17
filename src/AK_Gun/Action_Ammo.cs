using System;
using Peak.Afflictions;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AK_Gun;

public class Action_Ammo : ItemAction
{
    public bool consumeOnFullyUsed;

    // public override void RunAction() => item.photonView.RPC("ReduceUsesRPC", RpcTarget.All);

    [PunRPC]
    public void ReduceUsesRPC()
    {
        OptionableIntItemData data = item.GetData<OptionableIntItemData>(DataEntryKey.ItemUses);
        if (!data.HasData || data.Value <= 0)
            return;
        --data.Value;
        if (item.totalUses > 0)
            item.SetUseRemainingPercentage(data.Value / item.totalUses);
        if (data.Value != 0 || !consumeOnFullyUsed || !character || !character.IsLocal || !(character.data.currentItem == item))
            return;
        item.StartCoroutine(item.ConsumeDelayed());
    }
}