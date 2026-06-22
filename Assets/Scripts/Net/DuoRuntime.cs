using Photon.Pun;
using TR.Systems;

namespace TR.Net
{
    
    
    public static class DuoRuntime
    {
        
        public static bool IsDuo => MatchContext.IsDuo;

        
        public static bool IsSimulationAuthority =>
            !MatchContext.IsDuo || (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient);

        
        public static bool IsRemoteClient =>
            MatchContext.IsDuo && PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient;
    }
}
