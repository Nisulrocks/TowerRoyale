using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

namespace TR.Net
{
    
    
    public static class DuoPlayerColors
    {
        
        public static readonly Color Player1 = new Color(0.30f, 0.62f, 1f);   
        
        public static readonly Color Player2 = new Color(1f, 0.35f, 0.35f);   

        
        
        public static int GetPlayerSlot(int actorNumber)
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null) return 1;

            int lowest = int.MaxValue;
            foreach (var p in PhotonNetwork.PlayerList)
            {
                if (p.ActorNumber < lowest) lowest = p.ActorNumber;
            }
            return actorNumber <= lowest ? 1 : 2;
        }

        public static Color GetColorForActor(int actorNumber)
        {
            return GetPlayerSlot(actorNumber) == 1 ? Player1 : Player2;
        }

        public static Color GetLocalColor()
        {
            int actor = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : 1;
            return GetColorForActor(actor);
        }

        
        public static string ToHex(Color c)
        {
            return ColorUtility.ToHtmlStringRGB(c);
        }
    }
}
